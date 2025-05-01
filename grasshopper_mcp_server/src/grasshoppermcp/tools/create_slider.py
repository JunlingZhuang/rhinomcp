from mcp.server.fastmcp import Context
import json
from grasshoppermcp.server import get_grasshopper_connection, mcp, logger
from typing import Any, List, Dict

@mcp.tool()
def create_slider(
    ctx: Context,
    min: float = 0,
    max: float = 1,
    value: float = 0,
    x: float = 0,
    y: float = 0,
) -> str:
    """
    Create a slider in Grasshopper.
    
    Parameters:
    - min: Minimum value of the slider
    - max: Maximum value of the slider
    - value: Current value of the slider
    - x: x coordinate of the slider
    - y: y coordinate of the slider


    """
    try:
        # Get the global connection
        grasshopper = get_grasshopper_connection()

        command_params = {
            "min": min,
            "max": max,
            "value": value,
            "x": x,
            "y": y
        }

        # Create the object
        result = grasshopper.send_command("create_slider", command_params)  
        
        return f"Created slider: {result['name']}"
    except Exception as e:
        logger.error(f"Error creating object: {str(e)}")
        return f"Error creating object: {str(e)}"
 