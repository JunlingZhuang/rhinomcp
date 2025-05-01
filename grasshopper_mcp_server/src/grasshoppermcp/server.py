# grasshopper_mcp_server.py
from mcp.server.fastmcp import FastMCP, Context, Image
import socket
import json
import asyncio
import logging
from dataclasses import dataclass
from contextlib import asynccontextmanager
from typing import AsyncIterator, Dict, Any, List

# Configure logging
logging.basicConfig(level=logging.INFO, 
                    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s')
logger = logging.getLogger("GrasshopperMCPServer")

@dataclass
class GrasshopperConnection:
    host: str
    port: int
    sock: socket.socket | None = None  # Changed from 'socket' to 'sock' to avoid naming conflict
    
    def connect(self) -> bool:
        """Connect to the Grasshopper addon socket server"""
        if self.sock:
            return True
            
        try:
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.sock.connect((self.host, self.port))
            logger.info(f"Connected to Grasshopper at {self.host}:{self.port}")
            return True
        except Exception as e:
            logger.error(f"Failed to connect to Grasshopper: {str(e)}")
            self.sock = None
            return False
    
    def disconnect(self):
        """Disconnect from the Grasshopper addon"""
        if self.sock:
            try:
                self.sock.close()
            except Exception as e:
                logger.error(f"Error disconnecting from Grasshopper: {str(e)}")
            finally:
                self.sock = None

    def receive_full_response(self, sock, buffer_size=8192):
        """Receive the complete response, potentially in multiple chunks"""
        chunks = []
        # Use a consistent timeout value that matches the addon's timeout
        sock.settimeout(15.0)  # Match the addon's timeout
        
        try:
            while True:
                try:
                    chunk = sock.recv(buffer_size)
                    if not chunk:
                        # If we get an empty chunk, the connection might be closed
                        if not chunks:  # If we haven't received anything yet, this is an error
                            raise Exception("Connection closed before receiving any data")
                        break
                    
                    chunks.append(chunk)
                    
                    # Check if we've received a complete JSON object
                    try:
                        data = b''.join(chunks)
                        json.loads(data.decode('utf-8'))
                        # If we get here, it parsed successfully
                        logger.info(f"Received complete response ({len(data)} bytes)")
                        return data
                    except json.JSONDecodeError:
                        # Incomplete JSON, continue receiving
                        continue
                except socket.timeout:
                    # If we hit a timeout during receiving, break the loop and try to use what we have
                    logger.warning("Socket timeout during chunked receive")
                    break
                except (ConnectionError, BrokenPipeError, ConnectionResetError) as e:
                    logger.error(f"Socket connection error during receive: {str(e)}")
                    raise  # Re-raise to be handled by the caller
        except socket.timeout:
            logger.warning("Socket timeout during chunked receive")
        except Exception as e:
            logger.error(f"Error during receive: {str(e)}")
            raise
            
        # If we get here, we either timed out or broke out of the loop
        # Try to use what we have
        if chunks:
            data = b''.join(chunks)
            logger.info(f"Returning data after receive completion ({len(data)} bytes)")
            try:
                # Try to parse what we have
                json.loads(data.decode('utf-8'))
                return data
            except json.JSONDecodeError:
                # If we can't parse it, it's incomplete
                raise Exception("Incomplete JSON response received")
        else:
            raise Exception("No data received")

    def send_command(self, command_type: str, params: Dict[str, Any] = {}) -> Dict[str, Any]:
        """Send a command to Grasshopper and return the response"""
        if not self.sock and not self.connect():
            raise ConnectionError("Not connected to Grasshopper")
        
        command = {
            "type": command_type,
            "params": params or {}
        }
        
        try:
            # Log the command being sent
            logger.info(f"Sending command: {command_type} with params: {params}")

            if self.sock is None:
                raise Exception("Socket is not connected")
            
            # Send the command
            self.sock.sendall(json.dumps(command).encode('utf-8'))
            logger.info(f"Command sent, waiting for response...")
            
            # Set a timeout for receiving - use the same timeout as in receive_full_response
            self.sock.settimeout(15.0)  # Match the addon's timeout
            
            # Receive the response using the improved receive_full_response method
            response_data = self.receive_full_response(self.sock)
            logger.info(f"Received {len(response_data)} bytes of data")
            
            response = json.loads(response_data.decode('utf-8'))
            logger.info(f"Response parsed, status: {response.get('status', 'unknown')}")
            
            if response.get("status") == "error":
                logger.error(f"Grasshopper error: {response.get('message')}")
                raise Exception(response.get("message", "Unknown error from Grasshopper"))
            
            return response.get("result", {})
        except socket.timeout:
            logger.error("Socket timeout while waiting for response from Grasshopper")
            # Don't try to reconnect here - let the get_grasshopper_connection handle reconnection
            # Just invalidate the current socket so it will be recreated next time
            self.sock = None
            raise Exception("Timeout waiting for Grasshopper response - try simplifying your request")
        except (ConnectionError, BrokenPipeError, ConnectionResetError) as e:
            logger.error(f"Socket connection error: {str(e)}")
            self.sock = None
            raise Exception(f"Connection to Grasshopper lost: {str(e)}")
        except json.JSONDecodeError as e:
            logger.error(f"Invalid JSON response from Grasshopper: {str(e)}")
            # Try to log what was received
            if 'response_data' in locals() and response_data: # type: ignore
                logger.error(f"Raw response (first 200 bytes): {response_data[:200]}")
            raise Exception(f"Invalid response from Grasshopper: {str(e)}")
        except Exception as e:
            logger.error(f"Error communicating with Grasshopper: {str(e)}")
            # Don't try to reconnect here - let the get_grasshopper_connection handle reconnection
            self.sock = None
            raise Exception(f"Communication error with Grasshopper: {str(e)}")

@asynccontextmanager
async def server_lifespan(server: FastMCP) -> AsyncIterator[Dict[str, Any]]:
    """Manage server startup and shutdown lifecycle"""
    # We don't need to create a connection here since we're using the global connection
    # for resources and tools
    
    try:
        # Just log that we're starting up
        logger.info("GrasshopperMCP server starting up")
        
        # Try to connect to Grasshopper on startup to verify it's available
        try:
            # This will initialize the global connection if needed
            grasshopper = get_grasshopper_connection()
            logger.info("Successfully connected to Grasshopper on startup")
        except Exception as e:
            logger.warning(f"Could not connect to Grasshopper on startup: {str(e)}")
            logger.warning("Make sure the Grasshopper addon is running before using Grasshopper resources or tools")
        
        # Return an empty context - we're using the global connection
        yield {}
    finally:
        # Clean up the global connection on shutdown
        global _grasshopper_connection
        if _grasshopper_connection:
            logger.info("Disconnecting from Grasshopper on shutdown")
            _grasshopper_connection.disconnect()
            _grasshopper_connection = None
        logger.info("GrasshopperMCP server shut down")

# Create the MCP server with lifespan support
mcp = FastMCP(
    "GrasshopperMCP",
    description="Grasshopper integration through the Model Context Protocol",
    lifespan=server_lifespan
)

# Resource endpoints

# Global connection for resources (since resources can't access context)
_grasshopper_connection = None

def get_grasshopper_connection():
    """Get or create a persistent Grasshopper connection"""
    global _grasshopper_connection
    
    # Create a new connection if needed
    if _grasshopper_connection is None:
        _grasshopper_connection = GrasshopperConnection(host="127.0.0.1", port=2000)
        if not _grasshopper_connection.connect():
            logger.error("Failed to connect to Grasshopper")
            _grasshopper_connection = None
            raise Exception("Could not connect to Grasshopper. Make sure the Grasshopper addon is running.")
        logger.info("Created new persistent connection to Grasshopper")
    
    return _grasshopper_connection

# Main execution
def main():
    """Run the MCP server"""
    mcp.run()


if __name__ == "__main__":
    main()