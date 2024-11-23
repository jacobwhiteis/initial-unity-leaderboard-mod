# 		string text = Utils.getServerAddress() + "/getRecords/";
# 		text = text + "?track=" + course;
# 		text = text + "&layout=" + (layout ? "1" : "0");
# 		text = text + "&condition=" + (night ? "1" : "0");
# 		text = text + "&car=" + car;

# "https://www.initialunity.online/getRecords/?track=0&layout=0&condition=0&car=1"

# 8 trackIds
# 2 layoutIds
# 2 conditionIds
# 10 carIds
# ONLY VANILLA CARS AND TRACKS

import requests
import os
import json

# URLs
records_url = "https://initialunity.online/getRecords/?track=0&layout=0&condition=0&car=0"
ghost_url_template = "https://www.initialunity.online/getGhost/?id={id}"

# Output paths
records_output_file = "records.json"
ghosts_directory = "ghost_files"

# Create the directory for ghost files if it doesn't exist
os.makedirs(ghosts_directory, exist_ok=True)

# Fetch the records
try:
    response = requests.get(records_url)
    if response.status_code == 200:
        records_data = response.json()
        
        # Save the records JSON data to a file
        with open(records_output_file, "w", encoding="utf-8") as f:
            json.dump(records_data, f, ensure_ascii=False, indent=4)
        print(f"Records saved to {records_output_file}")

        # Download each ghost file based on the record ID
        for record in records_data.get("records", []):
            record_id = record["id"]
            ghost_url = ghost_url_template.format(id=record_id)
            ghost_response = requests.get(ghost_url)

            if ghost_response.status_code == 200:
                ghost_file_path = os.path.join(ghosts_directory, f"{record_id}.iureplay")

                # Save the ghost file
                with open(ghost_file_path, "wb") as ghost_file:
                    ghost_file.write(ghost_response.content)
                print(f"Downloaded ghost file for ID {record_id} and saved as {ghost_file_path}")
            else:
                print(f"Failed to download ghost file for ID {record_id}, status code: {ghost_response.status_code}")
    else:
        print(f"Failed to fetch records, status code: {response.status_code}")

except Exception as e:
    print("An error occurred:", str(e))
