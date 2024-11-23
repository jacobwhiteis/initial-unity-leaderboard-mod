import requests
import json

# API URL and API Key
API_URL = "https://bcnor7oy5b.execute-api.us-east-1.amazonaws.com/prod/leaderboard"
API_KEY = "yNrGPe5fnx1sMuNGXRV4o7JUyTonjAoH2G1ky6X3"

# Headers
headers = {
    "Content-Type": "application/json",
    "Accept": "application/json",
    "x-api-key": API_KEY
}

# JSON Body
data = {
    "track_id": "Akina",
    "time_in_ms": 298589,
    "user_id": "player1",
    "car_id": "AE86",
    "time_of_day": "day",
    "direction": "downhill"
}

# Make the POST request using the json parameter
response = requests.post(API_URL, headers=headers, json=data)

print(json.dumps(data))

# Check for empty response or JSON decoding error
try:
    response_data = response.json()
except json.JSONDecodeError:
    print("Failed to decode JSON response. Response text:")
    print(response.text)
    exit(1)

# Print the response
print(f"Status Code: {response.status_code}")
print(f"Response JSON: {response_data}")