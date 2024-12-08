# Change Log
All notable changes to this project will be documented in this file.

## [TODO]


- Improve website design
- Find obfuscation method / hide api key
- Write up a "Game update" process walkthrough
- Add steam chart-like graph
- Anti-cheat?
 
## [Unreleased]
 
## [1.0.1] - 2024-11-30

- Made local replays and ghosts read and write from new directory, to not interfere with old
- Fixed racing against leaderboard ghosts
- Updated driver name setting. Name now has a maximum length of 25 characters, and hex code colors are excluded.
- Fix hashtag bug
- Fix racing against leaderboard ghosts
- Allow names to be longer (have colors)
- Enable UI for uploading replays
- Change file extension of replays/ghosts
- Encrypt/compress replay files
- Put sector times in leaderboard submissions / replays
- Secure all endpoints - add api key verification and body/query string verification
- Make API actually check hardware id for better existing time
- Write migration / conversation script for replays
- Set up cloudflare
- Set up DNS / buy domain name
 

## [Released]
 
## [1.0.0] - 2024-11-29
 
Privately released
- Added viewing and submitting records to custom leaderboard site
- Added local and leaderboard replay functionality
- Added local ghost functionality