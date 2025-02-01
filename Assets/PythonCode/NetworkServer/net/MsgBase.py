import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__) , '..'))
import json
class MsgBase:
    def __init__(self):
        # Protocol name
        self.protoName = ""

    @staticmethod
    def encode(msgBase):
        """Encode the message object to a JSON string and then to bytes."""
        # Serialize the message object's dictionary to a JSON string
        s = json.dumps(msgBase.__dict__)
        # Encode the JSON string to bytes using UTF-8 encoding
        return s.encode('utf-8')

    @staticmethod
    def decode(protoName, bytes_data, offset, count):
        """Decode bytes to a message object based on the protoName."""
        # Decode the specified portion of bytes to a JSON string
        s = bytes_data[offset:offset+count].decode('utf-8')
        # Deserialize the JSON string to a dictionary
        data = json.loads(s)

        # Retrieve the message class from the registry
        try:
            from proto import Messages
            cls = getattr(Messages, protoName)
        except AttributeError:
            print(f"Message class not found: {protoName}")
            return None

        # Create an instance of the message class and update its attributes
        msg_base = cls()
        msg_base.__dict__.update(data)
        return msg_base

    @staticmethod
    def encode_name(msg_base):
        """Encode the protocol name with a 2-byte length prefix."""
        # Encode the protocol name to bytes using UTF-8
        name_bytes = msg_base.protoName.encode('utf-8')
        length = len(name_bytes)
        # Pack the length as a 2-byte little-endian integer
        length_bytes = length.to_bytes(2, byteorder='little')
        # Return the combined length and name bytes
        return length_bytes + name_bytes

    @staticmethod
    def decode_name(bytes_data, offset):
        """Decode the protocol name from bytes starting at the given offset."""
        # Must have at least 2 bytes for the length
        if offset + 2 > len(bytes_data):
            return "", 0

        # Read the 2-byte length prefix (little-endian)
        length_bytes = bytes_data[offset:offset+2]
        length = int.from_bytes(length_bytes, byteorder='little')

        # Ensure there's enough data for the name
        if offset + 2 + length > len(bytes_data):
            return "", 0

        # Extract and decode the protocol name
        name_bytes = bytes_data[offset+2:offset+2+length]
        name = name_bytes.decode('utf-8')
        count = 2 + length  # Total bytes read (length prefix + name)
        return name, count
