import sys
import os
sys.path.append(os.path.dirname(__file__) )
from net.MsgBase import MsgBase
from proto.Messages import MyMessage
if __name__ == "__main__": 
    
    # Register the message class
    # message_classes['MyMessage'] = MyMessage
    
    # Create a message instance
    msg = MyMessage()
    msg.content = "Hello, World!"

    # Encode the message
    encoded_msg = MsgBase.encode(msg)
    print(f"Encoded message bytes: {encoded_msg}")

    # Encode the message name
    encoded_name = MsgBase.encode_name(msg)
    print(f"Encoded name bytes: {encoded_name}")

    # Combine name and message bytes to form the full message
    full_message = encoded_name + encoded_msg
    print(f"Full message bytes: {full_message}")

    # Simulate receiving the message
    received_bytes = full_message

    # Decode the message name
    proto_name, name_count = MsgBase.decode_name(received_bytes, 0)
    print(f"Decoded protoName: {proto_name}, Bytes read for name: {name_count}")

    # Decode the message body
    msg_base = MsgBase.decode(proto_name, received_bytes, name_count, len(received_bytes) - name_count)
    if msg_base:
        print(f"Decoded message content: {msg_base.content}")