import gzip

def decompress_gz_file(input_file_path, output_file_path):
    """
    Decompress a .gz file and save the decompressed content to another file.

    :param input_file_path: Path to the input .gz file.
    :param output_file_path: Path to save the decompressed content.
    """
    try:
        with gzip.open(input_file_path, 'rb') as gz_file:
            with open(output_file_path, 'wb') as output_file:
                output_file.write(gz_file.read())
        print(f"Decompressed file saved to: {output_file_path}")
    except Exception as e:
        print(f"An error occurred: {e}")

# Example usage
input_gz_path = "/mnt/c/Users/jake/Downloads/000c01a3-5794-4d9f-8450-aa712bab8495.gz"  # Path to the .gz file
output_path = "decompressed_replay.txt"  # Path to save decompressed content
decompress_gz_file(input_gz_path, output_path)
