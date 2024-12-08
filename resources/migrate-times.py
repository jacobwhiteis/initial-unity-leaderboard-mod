from gevent import monkey
monkey.patch_all()
import requests
import json
import uuid
from gevent.pool import Pool

# Constants
base_url = "https://initialunity.online/getRecords/"
write_record_url = "https://o2hm4g1w50.execute-api.us-east-1.amazonaws.com/prod/writeRecord"
api_key = "ZTp93l4D6XaL3FZVH11Kd36CWbsrB5Y8aACFCfwv"
dummy_device_id = "dummy-device-id"
time_of_day = 0  # Fixed value

# Track, layout, and car ranges
track_ids = range(8)  # 0 to 7
layout_ids = range(2)  # 0 to 1
car_ids = range(10)  # 0 to 9

# Headers for writeRecord API
headers = {
    "x-api-key": api_key,
    "Content-Type": "application/json"
}

def upload_replay_data(s3_url, iuorep_file_path):
    """
    Upload the pre-compressed replay data to the S3 URL.
    """
    try:
        # Read and upload the file directly
        with open(iuorep_file_path, 'rb') as file_data:
            headers = {
                "Content-Type": "application/x-gzip"  # Indicate the file is already GZip-compressed
            }
            upload_response = requests.put(s3_url, data=file_data, headers=headers)
        
        if upload_response.status_code == 200:
            print(f"Replay data successfully uploaded: {iuorep_file_path}")
        else:
            print(f"Failed to upload replay data. Status Code: {upload_response.status_code}")
            print(upload_response.text)
    except FileNotFoundError:
        print(f"File not found: {iuorep_file_path}")
    except Exception as e:
        print(f"An error occurred during upload: {e}")

def make_requests(track, layout, car):
    """
    Process records for a specific track, layout, and car combination.
    """
    records_url = f"{base_url}?track={track}&layout={layout}&condition=-1&car={car}"
    
    try:
        response = requests.get(records_url)
        if response.status_code == 200:
            records_data = response.json()
            for record in records_data.get("records", []):
                write_record_data = {
                    "driverName": record["driver_name"],
                    "deviceId": str(uuid.uuid4()),
                    "timing": record["timing"],
                    "track": record["track"],
                    "direction": record["layout"],
                    "car": record["car"],
                    "timeOfDay": time_of_day,
                    "sector1": 0,
                    "sector2": 0,
                    "sector3": 0,
                    "sector4": 0
                }

                # Submit the record
                write_response = requests.post(write_record_url, json=write_record_data, headers=headers)
                if write_response.status_code == 200:
                    response_json = write_response.json()
                    s3_url = response_json['s3_url']
                    record_id = record["id"]
                    iuorep_file_path = f"/mnt/c/Users/jake/Documents/Initial Unity/ModGhosts/Conversion/{record_id}.iuorep"
                    
                    # Upload the replay data
                    upload_replay_data(s3_url, iuorep_file_path)
                else:
                    print(f"Failed to submit record: {write_record_data}, "
                          f"status code: {write_response.status_code}, "
                          f"details: {write_response.text}")
        else:
            print(f"Failed to fetch records from {records_url}, status code: {response.status_code}")
    except Exception as e:
        print(f"An error occurred while processing {records_url}: {e}")

def main():
    """
    Main entry point for processing records.
    """
    for track in track_ids:
        for layout in layout_ids:
            if track == 4 and layout == 0:
                continue
            pool = Pool(50)
            threads = [pool.spawn(make_requests, track, layout, car) for car in car_ids]
            pool.join()

if __name__ == '__main__':
    main()
