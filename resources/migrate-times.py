import requests
import json

# Constants
base_url = "https://initialunity.online/getRecords/"
write_record_url = "https://o2hm4g1w50.execute-api.us-east-1.amazonaws.com/prod/writeRecord"
api_key = "yNrGPe5fnx1sMuNGXRV4o7JUyTonjAoH2G1ky6X3"
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

# Iterate through all combinations
for track in track_ids:
    if track == 4:
        continue
    for layout in layout_ids:
        for car in car_ids:
            # Build the request URL
            records_url = f"{base_url}?track={track}&layout={layout}&condition=-1&car={car}"
            
            try:
                # Fetch records
                response = requests.get(records_url)
                if response.status_code == 200:
                    records_data = response.json()

                    # Process each record
                    for record in records_data.get("records", []):
                        # Prepare data for writeRecord API
                        write_record_data = {
                            "driverName": record["driver_name"],
                            "deviceId": dummy_device_id,
                            "timing": record["timing"],
                            "track": record["track"],
                            "direction": record["layout"],  # Map layout to direction
                            "car": record["car"],
                            "timeOfDay": time_of_day
                        }

                        # Submit the record to your API
                        write_response = requests.post(
                            write_record_url,
                            json=write_record_data,
                            headers=headers  # Include the API key in headers
                        )

                        if write_response.status_code == 200:
                            print(f"Successfully submitted record: {write_record_data}")
                        else:
                            print(f"Failed to submit record: {write_record_data}, "
                                  f"status code: {write_response.status_code}")
                else:
                    print(f"Failed to fetch records from {records_url}, status code: {response.status_code}")
            except Exception as e:
                print(f"An error occurred while processing {records_url}: {e}")
