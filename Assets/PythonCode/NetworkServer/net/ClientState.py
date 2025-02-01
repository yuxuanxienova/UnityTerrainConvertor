import os
import sys
sys.path.append(os.path.join(os.path.dirname(__file__) , '..'))
import socket
import time
from net.ByteArray import ByteArray
class ClientState:
    def __init__(self,client_socket:socket.socket):
        self.client_address = client_socket.getpeername()
        self.socket:socket.socket = client_socket
        self.read_buffer=ByteArray()
        self.last_active_time = time.time()