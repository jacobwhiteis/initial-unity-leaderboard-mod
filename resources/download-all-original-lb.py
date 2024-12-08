from gevent import monkey
monkey.patch_all()
import os
import json
import requests
from gevent import pool

# Base URLs
records_url_template = "https://initialunity.online/getRecords/?track={track}&layout={layout}&condition={condition}&car={car}"

# Directory for output files
output_directory = "output_records"
os.makedirs(output_directory, exist_ok=True)

# Shared structure to store all IDs
all_ids = []

# Define a function to process each parameter combination
def fetch_and_process(track, layout, condition, car):
    records_url = records_url_template.format(track=track, layout=layout, condition=condition, car=car)
    try:
        response = requests.get(records_url)
        if response.status_code == 200:
            records_data = response.json()

            # Extract IDs from the records
            if "records" in records_data:
                ids = [record["id"] for record in records_data["records"]]
                all_ids.extend(ids)
                print(f"Fetched {len(ids)} IDs for track={track}, layout={layout}, condition={condition}, car={car}")
            else:
                print(f"No records found for track={track}, layout={layout}, condition={condition}, car={car}")

        else:
            print(f"Failed to fetch records for URL: {records_url}, status code: {response.status_code}")

    except Exception as e:
        print(f"An error occurred while processing URL {records_url}: {str(e)}")

# Use gevent pool to make concurrent requests
def main():
    args = [
        (track, layout, condition, car)
        for track in range(8)  # track 0-7
        for layout in range(2)  # layout 0-1
        for condition in range(2)  # condition 0-1
        for car in range(10)  # car 0-9
    ]

    # Create a pool of workers
    pool_size = 50
    pool_instance = pool.Pool(pool_size)

    # Spawn tasks in the pool
    threads = [pool_instance.spawn(fetch_and_process, *arg) for arg in args]

    # Wait for all tasks to complete
    pool_instance.join()

    output_file = os.path.join(output_directory, "record_ids_full.json")
    with open(output_file, "w", encoding="utf-8") as f:
        json.dump(all_ids, f, ensure_ascii=False, indent=4)

    print(f"All IDs written")

if __name__ == "__main__":
    main()
