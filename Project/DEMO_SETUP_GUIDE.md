# Demo Setup Guide — AI-Powered Resume Screening System

This guide explains how to make the application accessible for demo purposes from another device — either on the same network (LAN/Wi-Fi) or from a completely different network (remote/internet).

---

## Prerequisites

Before starting, ensure the following are running on your PC:

**Terminal 1 — Backend API:**
```bash
cd "A:\MCA Project\Project\ResumeScreening.API"
dotnet run
```
API runs on: http://localhost:5109

**Terminal 2 — Frontend Angular:**
```bash
cd "A:\MCA Project\Project\resume-screening-ui"
ng serve
```
UI runs on: http://localhost:4200

**Default Login Credentials:**
- Email: admin@resumescreening.com
- Password: Admin@123

---

## Option 1: Same Network (LAN / Wi-Fi)

Use this when the demo device (laptop, phone, tablet) is connected to the **same Wi-Fi or LAN** as your PC.

### Step 1: Find Your PC's IP Address

Open Command Prompt or PowerShell and run:
```
ipconfig
```
Look for **IPv4 Address** under your active adapter (Wi-Fi or Ethernet).
Example: `192.168.0.104`

### Step 2: Configure API to Listen on All Interfaces

Edit `ResumeScreening.API/Properties/launchSettings.json`:
```json
{
  "profiles": {
    "ResumeScreening.API": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "",
      "applicationUrl": "http://0.0.0.0:5109",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```
**Key change:** `applicationUrl` set to `http://0.0.0.0:5109` (listens on all network interfaces, not just localhost).

### Step 3: Update CORS Policy

Edit `ResumeScreening.API/appsettings.json` — add your IP to AllowedOrigins:
```json
"AllowedOrigins": "http://localhost:4200;https://localhost:4200;http://127.0.0.1:4200;http://192.168.0.104:4200"
```
Replace `192.168.0.104` with your actual IP.

### Step 4: Configure Angular to Listen on All Interfaces

Edit `resume-screening-ui/angular.json` — change the host under `serve > options`:
```json
"serve": {
  "builder": "@angular-devkit/build-angular:dev-server",
  "options": {
    "host": "0.0.0.0",
    "port": 4200,
    "open": true
  }
}
```
**Key change:** `host` set to `"0.0.0.0"` instead of `"127.0.0.1"`.

### Step 5: Allow Through Windows Firewall

Open PowerShell as Administrator and run:
```powershell
New-NetFirewallRule -DisplayName "Resume Screening API" -Direction Inbound -LocalPort 5109 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "Resume Screening UI" -Direction Inbound -LocalPort 4200 -Protocol TCP -Action Allow
```

### Step 6: Access from Other Device

On the other device's browser, open:
```
http://192.168.0.104:4200
```
Replace with your actual IP address.

### Reverting After Demo

To go back to local-only development:
1. Change `"host": "0.0.0.0"` back to `"host": "127.0.0.1"` in `angular.json`
2. Change `applicationUrl` back to `"https://localhost:7109;http://localhost:5109"` in `launchSettings.json`
3. Optionally remove the firewall rules:
```powershell
Remove-NetFirewallRule -DisplayName "Resume Screening API"
Remove-NetFirewallRule -DisplayName "Resume Screening UI"
```

---

## Option 2: Different Network (Remote / Internet)

Use this when the demo device is on a **completely different network** — e.g., reviewer is at home, you are at college, or demo over a video call.

This uses **ngrok**, a free tunneling tool that creates a public URL pointing to your local machine.

### Step 1: Install ngrok

1. Go to https://ngrok.com and create a free account
2. Download ngrok for Windows from https://ngrok.com/download
3. Extract the zip — you'll get `ngrok.exe`
4. Move `ngrok.exe` to a convenient location (e.g., `C:\ngrok\ngrok.exe`)
5. Add it to your PATH, or run it from the folder directly

### Step 2: Authenticate ngrok (One-Time Setup)

After signing up, go to https://dashboard.ngrok.com/get-started/your-authtoken and copy your auth token.

Run:
```bash
ngrok config add-authtoken YOUR_AUTH_TOKEN_HERE
```

### Step 3: Start Your Application

Make sure both API and Angular are running locally (see Prerequisites above).

### Step 4: Create ngrok Tunnels

You need to expose **both** ports. Open two new terminals:

**Terminal 3 — Tunnel for API (port 5109):**
```bash
ngrok http 5109
```
This will show output like:
```
Forwarding  https://abc123.ngrok-free.app -> http://localhost:5109
```
Note down the **API tunnel URL** (e.g., `https://abc123.ngrok-free.app`).

**Terminal 4 — Tunnel for Angular UI (port 4200):**
```bash
ngrok http 4200
```
This will show output like:
```
Forwarding  https://xyz789.ngrok-free.app -> http://localhost:4200
```
Note down the **UI tunnel URL** (e.g., `https://xyz789.ngrok-free.app`).

### Step 5: Update Configuration for ngrok URLs

**5a. Update CORS to allow the Angular ngrok URL:**

Edit `ResumeScreening.API/appsettings.json`:
```json
"AllowedOrigins": "http://localhost:4200;https://localhost:4200;http://127.0.0.1:4200;https://xyz789.ngrok-free.app"
```
Replace `xyz789.ngrok-free.app` with your actual Angular ngrok URL.

**5b. Update Angular proxy to point to API ngrok URL:**

Edit `resume-screening-ui/proxy.conf.json`:
```json
{
  "/api": {
    "target": "https://abc123.ngrok-free.app",
    "secure": true,
    "changeOrigin": true
  }
}
```
Replace `abc123.ngrok-free.app` with your actual API ngrok URL.

**5c. Restart both API and Angular** after making these changes.

### Step 6: Access from Any Device Anywhere

Share the Angular ngrok URL with the reviewer:
```
https://xyz789.ngrok-free.app
```
They can open this in any browser from anywhere in the world.

### ngrok Free Tier Limitations

- URLs change every time you restart ngrok (paid plan gives fixed subdomains)
- ngrok shows an interstitial "Visit Site" page on first access — just click through
- Limited to 1 tunnel per agent on free plan (use two terminals, or upgrade)
- Rate limited to ~40 requests/minute on free tier (sufficient for demo)

### Reverting After Demo

1. Stop ngrok (Ctrl+C in both terminals)
2. Revert `proxy.conf.json` back to:
```json
{
  "/api": {
    "target": "http://localhost:5109",
    "secure": false,
    "changeOrigin": true,
    "logLevel": "debug"
  }
}
```
3. Remove the ngrok URL from `AllowedOrigins` in `appsettings.json`

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "Connection refused" from other device | Check firewall rules are added; verify API is running on 0.0.0.0 |
| CORS error in browser console | Ensure the ngrok/IP URL is added to AllowedOrigins in appsettings.json, then restart API |
| Angular page loads but API calls fail | Check proxy.conf.json target is correct; restart ng serve after changes |
| ngrok shows "ERR_NGROK_6024" | Free tier allows 1 agent; open a second terminal for the second tunnel |
| "This site can't be reached" on other device | Ensure both devices are on the same Wi-Fi (Option 1); or check ngrok is running (Option 2) |
| Slow performance over ngrok | Normal — traffic routes through ngrok servers. Use Option 1 (same network) for better speed |

---

## Quick Reference

| Setup | Your PC | Other Device |
|-------|---------|-------------|
| **Same Network** | Run API on 0.0.0.0:5109 + Angular on 0.0.0.0:4200 | Open http://YOUR_IP:4200 |
| **Different Network** | Run locally + ngrok on both ports | Open https://xyz.ngrok-free.app |
