import os
import json

# Function to check if files corresponding to IDs exist in a given directory
def find_missing_files(json_file_path, directory_path, output_file="missing_ids.json"):
    # Load the JSON file
    with open(json_file_path, 'r') as json_file:
        ids = json.load(json_file)
    
    # List to store missing IDs
    missing_ids = []
    
    # Simple check
    directory_path = "/mnt/c/Users/jake/Documents/Initial Unity/ModGhosts/Conversion"
    print("Directory exists:", os.path.exists(directory_path))

    # Check each ID for corresponding file
    i = 0
    for file_id in ids:
        if i % 100 == 0:
            print(f"Done {i}")
        i += 1

        file_name = f"{file_id}.iuorep"
        file_path = os.path.join(directory_path, file_name)

        if not os.path.exists(file_path):
            if file_id == 2080441:
                print(file_path)
            missing_ids.append(file_id)
    
    # Output the missing IDs to a JSON file
    with open(output_file, 'w') as output:
        json.dump(missing_ids, output, indent=4)
    
    print(f"Missing IDs saved to {output_file}")

# Example usage
json_file_path = "output_records/record_ids_full.json"  # Replace with your JSON file path
directory_path = "/mnt/c/Users/jake/Documents/Initial Unity/ModGhosts/Conversion"  # Replace with your directory path
find_missing_files(json_file_path, directory_path)
