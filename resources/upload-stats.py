import requests
import json
import os

# API URL and key
API_URL = "https://bcnor7oy5b.execute-api.us-east-1.amazonaws.com/prod/leaderboard"
API_KEY = "yNrGPe5fnx1sMuNGXRV4o7JUyTonjAoH2G1ky6X3"  # Fill in your API key
RECORDS_FILE = "records.json"
GHOSTS_DIRECTORY = "ghost_files"

# Headers for the POST request
headers_post = {
    "Content-Type": "application/json",
    "Accept": "application/json",
    "x-api-key": API_KEY
}
headers_s3 = {
    'Content-Type': 'application/octet-stream'
}

# Mappings
track_map = {
    0: "Akina",
    1: "Tsuchisaka",
    2: "Tsubaki",
    3: "Irohazaka",
    4: "Akagi",
    5: "Usui",
    6: "Myogi",
    7: "Sadamine"
}

car_map = {
    0: "AE86",
    1: "FD3S",
    2: "RPS13",
    3: "BNR32",
    4: "CE9A",
    5: "EG6",
    6: "SW20",
    7: "FC3S",
    8: "AP1",
    9: "NA6E"
}

condition_map = {
    0: "day",
    1: "night"
}

layout_map = {
    0: "downhill",
    1: "uphill"
}

# Helper function to upload the ghost file to the presigned S3 URL
def upload_ghost_file(s3_url, ghost_file_path):
    try:
        with open(ghost_file_path, "rb") as file:
            response = requests.put(s3_url, headers=headers_s3, data=file)
            if response.status_code == 200:
                print(f"Successfully uploaded {ghost_file_path}")
            else:
                print(f"Failed to upload {ghost_file_path}, status code: {response.status_code}")
                print(s3_url)
    except Exception as e:
        print(f"An error occurred while uploading {ghost_file_path}: {str(e)}")

# Function to process records and make POST requests
def process_records():
    try:
        # Load records from the JSON file
        with open(RECORDS_FILE, "r", encoding="utf-8") as f:
            records_data = json.load(f)

        # Iterate over each record
        for record in records_data.get("records", []):
            # Map the fields
            track_id = track_map.get(record["track"], "Unknown")
            car_id = car_map.get(record["car"], "Unknown")
            time_of_day = condition_map.get(record["condition"], "Unknown")
            direction = layout_map.get(record["layout"], "Unknown")
            time_in_ms = int(record["timing"] * 1000)  # Convert time from seconds to milliseconds
            user_id = record["driver_name"]

            # Prepare the data body for the POST request
            data = {
                "track_id": track_id,
                "time_in_ms": time_in_ms,
                "user_id": user_id,
                "car_id": car_id,
                "time_of_day": time_of_day,
                "direction": direction
            }

            try:
                # Make the POST request to submit the record
                print(data)
                response = requests.post(API_URL, headers=headers_post, json=data)
                if response.status_code == 200:              
                    response_data = response.json()
                    print(response.text)
                    s3_url = response_data.get("presigned_s3_url")

                    if s3_url:
                        # Determine the corresponding ghost file path
                        ghost_file_path = os.path.join(GHOSTS_DIRECTORY, f"{record['id']}.iureplay")

                        # Check if the ghost file exists and upload it
                        if os.path.exists(ghost_file_path):
                            upload_ghost_file(s3_url, ghost_file_path)
                        else:
                            print(f"Ghost file not found for ID {record['id']}")
                    else:
                        print(f"No presigned S3 URL returned for record ID {record['id']}")
                else:
                    print(f"Failed to submit record ID {record['id']}, status code: {response.status_code}")

            except Exception as e:
                print(response.text)
                print(f"An error occurred while processing record ID {record['id']}: {str(e)}")

    except FileNotFoundError:
        print(f"Records file '{RECORDS_FILE}' not found.")
    except json.JSONDecodeError:
        print("Failed to parse the JSON data from the records file.")
    except Exception as e:
        print(f"An unexpected error occurred: {str(e)}")

# Main entry point
if __name__ == "__main__":
    process_records()

