import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__) , '..'))
import socket
import select
import time
from typing import List, Dict, Callable
from net.ClientState import ClientState
from net.ByteArray import ByteArray
from net.MsgBase import MsgBase
from proto.Messages import MsgPing, MsgPong , MsgTest
from prometheus_client import Counter, Gauge, Histogram, start_http_server
# Define listener types
EventHandler = Callable[[str], None]
MsgHandler = Callable[[MsgBase], None]
class NetManagerServer:
    def __init__(self) -> None: 
        # Listening socket
        self.listen_fd = None

        # Client sockets and states
        self.clients:Dict[socket.socket,ClientState] = {}

        # Select read list
        self.check_read = []

        # Ping interval
        self.ping_interval = 30
        
        # Event handling
        self.event_handlers:Dict[str,EventHandler] ={}
        self.message_handlers:Dict[str,MsgHandler] ={} 
        
        self.add_event_handler("on_disconnect",self.on_disconnect)
        self.add_event_handler("on_timer",self.on_timer)
        self.add_event_handler("check_ping",self.check_ping)
        
        self.add_msg_handler("MsgPing",self.handle_msg_ping)
        
        # Prometheus metrics
        self.active_connections = Gauge('active_connections', 'Number of active connections')
        self.messages_received = Counter('messages_received', 'Total number of messages received')
        self.messages_sent = Counter('messages_sent', 'Total number of messages sent')
        self.message_processing_time = Histogram('message_processing_time_seconds', 'Time spent processing messages')

        # Start the Prometheus metrics HTTP server on port 8000
        start_http_server(8000)

#---------------------------------监听结构------------------------------------------
    #------------------------------1. 网络事件监听----------------------------------------
    # Add event listener
    def add_event_handler(self, event_name: str, handler: EventHandler):
        if event_name not in self.event_handlers:
            self.event_handlers[event_name] = []
        self.event_handlers[event_name].append(handler)
    # Remove event listener
    def remove_event_handler(self, event_name: str, handler: EventHandler):
        if event_name in self.event_handlers:
            self.event_handlers[event_name].remove(handler)
            if not self.event_handlers[event_name]:
                del self.event_handlers[event_name]
    # Fire event
    def fire_event(self, event_name: str, err: str):
        if event_name in self.event_handlers:
            for handler in self.event_handlers[event_name]:
                handler(err)

    def on_disconnect(self,err:str):
        print("Close")


    def on_timer(self,err:str):
        self.check_ping(err)

    # Ping check
    def check_ping(self,err:str):
        # Current timestamp
        time_now = self.get_time_stamp()

        # Iterate and remove
        for s in list(self.clients.values()):
            if time_now - s.last_ping_time > self.ping_interval * 4:
                print(f"Ping Close {s.socket.getpeername()}")
                self.close(s)
                # Since 'close' removes the client from the list,
                # we break out of the loop to avoid iteration errors
                self.active_connections.dec()  # Decrement active connections
                break
    #-------------------------------2. 网络消息监听---------------------------------------------
    # Add message listener
    def add_msg_handler(self, msg_name: str, handler: MsgHandler):
        if msg_name not in self.message_handlers:
            self.message_handlers[msg_name] = []
        self.message_handlers[msg_name].append(handler)
    # Remove message listener
    def remove_msg_handler(self, msg_name: str, handler: MsgHandler):
        if msg_name in self.message_handlers:
            self.message_handlers[msg_name].remove(handler)
            if not self.message_handlers[msg_name]:
                del self.message_handlers[msg_name]
    # Fire message
    def fire_msg(self, msg_name: str, client_state:ClientState ,msg_base: MsgBase):
        if msg_name in self.message_handlers:
            for handler in self.message_handlers[msg_name]:
                handler(client_state , msg_base)
                
    def handle_msg_ping(self, client:ClientState, msg_base:MsgBase):
        print("MsgPing")
        client.last_ping_time = self.get_time_stamp()
        msg_pong = MsgPong()
        self.send(client, msg_pong)
        
    #-------------------------------------------------------------------------------------
    def initialize(self,listen_port):
        # Create socket
        self.listen_fd = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.listen_fd.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)

        # Bind
        self.listen_fd.bind(('0.0.0.0', listen_port))

        # Listen
        self.listen_fd.listen()
        
        #
        self.active_connections.set(0)
        print("Server started successfully")
    def update(self):
        self.reset_check_read()

        # Use select to wait for sockets to be ready
        read_sockets, _, _ = select.select(self.check_read, [], [], 1)

        for s in read_sockets:
            if s == self.listen_fd:
                self.read_listen_fd()
            else:
                self.read_client_fd(s)

        # Handle timeouts
        self.timer()

    
    def reset_check_read(self):
        self.check_read = [self.listen_fd]
        for state in self.clients.values():
            self.check_read.append(state.socket)

    
    def read_listen_fd(self):
        try:
            client_fd, addr = self.listen_fd.accept()
            print(f"Accepted connection from {addr}")
            state = ClientState(client_socket=client_fd)
            state.last_ping_time = self.get_time_stamp()
            self.clients[client_fd] = state
            
            self.active_connections.inc()  # Increment active connections
        except socket.error as ex:
            print(f"Accept failed: {ex}")

    
    def read_client_fd(self,client_fd):
        client_state:ClientState = self.clients.get(client_fd)
        client_read_buffer = client_state.read_buffer
        
        #缓冲区不够，清除，若依旧还不够，只能返回
        #缓冲区长度只有1024， 单条协议超过缓冲区长度时会发生错误，根据需要调整长度
        # print("[INFO][Net Server][Client:{0}]Client Read Buffer Remain:{1}".format(client_state.client_address,client_read_buffer.remain))
        if client_read_buffer.remain <= 0:
            client_read_buffer.resize(client_read_buffer.capacity + 1024)
            self.on_receive_data(client_state)
            client_read_buffer.check_and_move_bytes()
            
        if client_read_buffer.remain <= 0:
            print("[ERROR][Net Server]Receive Fail, mabe msg length > buff capacity")
            self.close(client_state)
            self.active_connections.dec()  # Decrement active connections
            return
       
        try:
            # Receive data
            data = client_fd.recv(client_read_buffer.remain)
            # print("[INFO][NetManagerServer]data:{0}".format(data))
            
            # Check for disconnection, win系统下，客户端关闭连接时，会返回空数据
            if not data:
                print(f"Client disconnected: {client_fd.getpeername()}")
                self.close(client_state)
                return
            # Write data to buffer
            client_read_buffer.write(data, 0, len(data))

            # Process received data
            self.on_receive_data(client_state)

            # Move buffer if necessary
            client_read_buffer.check_and_move_bytes()

        except socket.error as ex:
            print(f"[ERROR][Net Server]Receive socket exception: {ex}")
            self.close(client_state)
            self.active_connections.dec()  # Decrement active connections

    
    def close(self,state):
        # Event handling
        self.fire_event("on_disconnect", "")

        # Close socket and remove from clients
        state.socket.close()
        self.active_connections.dec()  # Decrement active connections
        
        del self.clients[state.socket]
        
        

    
    def on_receive_data(self,state):
        read_buffer = state.read_buffer

        while True:
            # Check if we have enough data for the message length
            if read_buffer.length < 2:
                break

            # Peek message length
            body_length = int.from_bytes(read_buffer.bytes[read_buffer.read_idx:read_buffer.read_idx+2], byteorder='little')

            # Check if we have the full message
            if read_buffer.length < 2 + body_length:
                break

            read_buffer.read_idx += 2  # Move past the length field

            # Decode message name
            proto_name, name_count = MsgBase.decode_name(read_buffer.bytes, read_buffer.read_idx)
            if not proto_name:
                print("Failed to decode message name")
                self.close(state)
                self.active_connections.dec()  # Decrement active connections
                return

            read_buffer.read_idx += name_count

            # Decode message body
            body_count = body_length - name_count
            msg_base = MsgBase.decode(proto_name, read_buffer.bytes, read_buffer.read_idx, body_count)
            read_buffer.read_idx += body_count
            
            # Start a timer to measure message processing time
            start_time = time.time()

            # Handle message
            # print(f"Received message: {proto_name}")

            if proto_name in self.message_handlers:
                self.fire_msg(proto_name, state , msg_base)
            else:
                print(f"No handler for message: {proto_name}")
                
            # Update metrics
            self.messages_received.inc()
            # Record the processing time
            elapsed_time = time.time() - start_time
            self.message_processing_time.observe(elapsed_time)

            # Move buffer if necessary
            read_buffer.check_and_move_bytes()

    
    def timer(self):
        # Event handling
        self.fire_event("on_timer", "")

    
    def send(self,cs, msg):
        # Check state
        if cs is None or cs.socket.fileno() == -1:
            return

        # Encode data
        name_bytes = MsgBase.encode_name(msg)
        body_bytes = MsgBase.encode(msg)
        length = len(name_bytes) + len(body_bytes)
        send_bytes = bytearray(2 + length)

        # Pack length
        send_bytes[0:2] = length.to_bytes(2, byteorder='little')

        # Pack name
        send_bytes[2:2+len(name_bytes)] = name_bytes

        # Pack body
        send_bytes[2+len(name_bytes):] = body_bytes

        # Send data
        try:
            cs.socket.sendall(send_bytes)
        except socket.error as ex:
            print(f"Socket closed on sendall: {ex}")
            self.close(cs)
            self.active_connections.dec()# Decrement active connections
        
        self.messages_sent.inc()

    @staticmethod
    def get_time_stamp():
        return int(time.time())
if __name__ == "__main__":
    SERVER_PORT = 65432;
    server = NetManagerServer()
    
    def handle_msg_test(client_state:ClientState,msg_base:MsgBase):
        print("MsgTest"+msg_base.str + str(msg_base.num))
    server.add_msg_handler("MsgTest",handle_msg_test)
    
    server.initialize(SERVER_PORT)
    while True:
        server.update()
        time.sleep(0.01)