"""Grasshopper integration through the Model Context Protocol."""

__version__ = "0.1.0"


from .server import GrasshopperConnection, get_grasshopper_connection, mcp, logger


from .tools.create_slider import create_slider