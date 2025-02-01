import math

class ByteArray:
    # Default size
    DEFAULT_SIZE = 1024

    def __init__(self, size=None, default_bytes=None):
        # Initialize from default bytes
        if default_bytes is not None:
            self.bytes = bytearray(default_bytes)
            self.capacity = len(self.bytes)
            self.init_size = self.capacity
            self.read_idx = 0
            self.write_idx = self.capacity
        else:
            # Initialize with specified size or default size
            self.init_size = size if size is not None else self.DEFAULT_SIZE
            self.bytes = bytearray(self.init_size)
            self.capacity = self.init_size
            self.read_idx = 0
            self.write_idx = 0

    @property
    def remain(self):
        """Remaining capacity."""
        return self.capacity - self.write_idx

    @property
    def length(self):
        """Length of data available to read."""
        return self.write_idx - self.read_idx

    def resize(self, size):
        """Resize the buffer to accommodate more data."""
        if size < self.length or size < self.init_size:
            return

        # Increase capacity to the next power of two greater than size
        n = 1
        while n < size:
            n *= 2

        self.capacity = n
        new_bytes = bytearray(self.capacity)
        # Copy existing data to the new buffer
        new_bytes[0:self.length] = self.bytes[self.read_idx:self.write_idx]
        self.bytes = new_bytes
        self.write_idx = self.length
        self.read_idx = 0

    def check_and_move_bytes(self):
        """Check and move data if necessary to prevent buffer overflow."""
        if self.length < 8:
            self.move_bytes()

    def move_bytes(self):
        """Move unread data to the beginning of the buffer."""
        self.bytes[0:self.length] = self.bytes[self.read_idx:self.write_idx]
        self.write_idx = self.length
        self.read_idx = 0

    def write(self, bs, offset, count):
        """Write data into the buffer."""
        if self.remain < count:
            self.resize(self.length + count)
        self.bytes[self.write_idx:self.write_idx+count] = bs[offset:offset+count]
        self.write_idx += count
        return count

    def read(self, bs, offset, count):
        """Read data from the buffer."""
        count = min(count, self.length)
        bs[offset:offset+count] = self.bytes[self.read_idx:self.read_idx+count]
        self.read_idx += count
        self.check_and_move_bytes()
        return count

    def read_int16(self):
        """Read a 16-bit integer from the buffer."""
        if self.length < 2:
            return 0
        ret = int.from_bytes(self.bytes[self.read_idx:self.read_idx+2], byteorder='little', signed=True)
        self.read_idx += 2
        self.check_and_move_bytes()
        return ret

    def read_int32(self):
        """Read a 32-bit integer from the buffer."""
        if self.length < 4:
            return 0
        ret = int.from_bytes(self.bytes[self.read_idx:self.read_idx+4], byteorder='little', signed=True)
        self.read_idx += 4
        self.check_and_move_bytes()
        return ret

    def __str__(self):
        """Return a string representation of the buffer (for debugging)."""
        return ' '.join('{:02X}'.format(b) for b in self.bytes[self.read_idx:self.write_idx])

    def debug(self):
        """Return detailed debug information about the buffer."""
        return f"readIdx({self.read_idx}) writeIdx({self.write_idx}) bytes({self.bytes.hex().upper()})"



    


if __name__ == "__main__":

    # Create a ByteArray instance with default size
    byte_array = ByteArray()
    print("Created ByteArray with default size.")

    # Test writing data into the buffer
    data_to_write = b'Hello, World!'  # 13 bytes
    bytes_written = byte_array.write(data_to_write, 0, len(data_to_write))
    print(f"Bytes written: {bytes_written}")

    # Test length and remain properties
    print(f"Buffer length after write: {byte_array.length}")
    print(f"Buffer remain after write: {byte_array.remain}")

    # Test __str__ method
    print(f"Buffer content: {byte_array}") # Content should be '48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21'， check ASCII table

    # Test reading data from the buffer
    read_buffer = bytearray(13)
    bytes_read = byte_array.read(read_buffer, 0, 5)  # Read first 5 bytes
    print(f"Bytes read: {bytes_read}")
    print(f"Data read: {read_buffer[:bytes_read].decode('utf-8')}")

    # Test length and remain properties after read
    print(f"Buffer length after read: {byte_array.length}")
    print(f"Buffer remain after read: {byte_array.remain}")

    # Test reading an int16 from the buffer
    # Write two bytes representing an int16
    byte_array = ByteArray()
    int16_value = 12345
    int16_bytes = int16_value.to_bytes(2, byteorder='little', signed=True)
    byte_array.write(int16_bytes, 0, 2)
    print(f"Written int16 value: {int16_value}")

    # Read int16 value
    read_int16_value = byte_array.read_int16()
    print(f"Read int16 value: {read_int16_value}")

    # Test reading an int32 from the buffer
    # Write four bytes representing an int32
    byte_array = ByteArray()
    int32_value = 123456789
    int32_bytes = int32_value.to_bytes(4, byteorder='little', signed=True)
    byte_array.write(int32_bytes, 0, 4)
    print(f"Written int32 value: {int32_value}")

    # Read int32 value
    read_int32_value = byte_array.read_int32()
    print(f"Read int32 value: {read_int32_value}")

    # Test buffer resizing by writing a large amount of data
    byte_array = ByteArray()
    large_data = b'A' * 2000  # 2000 bytes
    bytes_written = byte_array.write(large_data, 0, len(large_data))
    print(f"Bytes written after resizing: {bytes_written}")
    print(f"Buffer capacity after resizing: {byte_array.capacity}")

    # Test move_bytes and check_and_move_bytes methods
    # Read some data to increase read_idx
    read_buffer = bytearray(100)
    bytes_read = byte_array.read(read_buffer, 0, 100)
    print(f"Bytes read for move test: {bytes_read}")
    print(f"Buffer length before move: {byte_array.length}")

    # Move unread bytes to the beginning of the buffer
    byte_array.move_bytes()
    print(f"Buffer length after move: {byte_array.length}")
    print(f"Buffer content after move: {byte_array}")

    # Test debug method
    print(f"Debug info: {byte_array.debug()}")
    
    
 

    




