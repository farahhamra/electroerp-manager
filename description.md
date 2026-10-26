# ElectroERP Manager — Product Description

## What the Product Is

**ElectroERP Manager** is a lightweight Windows desktop application built with C# and the .NET framework. Installed directly on the host server PC, it serves as the dedicated local management, security, and process supervisor wrapper for the full ElectroERP application (both Node.js backend API and Next.js frontend web app), allowing administrators to safely control server execution and monitor error logs without terminal access.

---

## What Problem It Solves

Running on-premise business software on a local server PC presents common operational challenges:

- **Accidental Server Terminations**: Terminal windows or unshielded scripts can be accidentally closed by staff using the server machine, halting business operations.
- **Lack of Local Access Control**: Anyone with physical access to the server PC could unintentionally stop, restart, or alter server operations.
- **Unattended Downtime on Crashes & Reboots**: Server unexpected crashes or PC reboots leave store systems offline until someone manually notices and relaunches the service.
- **Scattered Error Diagnostics**: Uncaught backend exceptions and crash logs get lost in transient console outputs, making troubleshooting difficult.

ElectroERP Manager addresses these issues by wrapping the full-stack server processes inside a PIN-protected, resilient desktop GUI that runs quietly in the system tray, automatically restarts on crashes, and captures structured error logs.

---

## Authentication & Security Model

- **6-Digit PIN Authentication**: Security is governed exclusively by a 6-digit numeric PIN (default: `123456`) with no username requirement.
- **Login Protection**: Requires PIN entry to unlock and access the management interface.
- **Action Confirmation**: Entering the correct PIN is mandatory before executing critical lifecycle operations:
  - Launching the ElectroERP server.
  - Stopping the ElectroERP server.
- **PIN Management**: Administrators can securely update the 6-digit PIN directly from the Settings page by validating the existing PIN.

---

## Application Structure & Pages

The application is structured around 3 dedicated pages:

```
┌────────────────────────────────────────────────────────────────────────┐
│                          ElectroERP Manager                            │
├────────────────────────────────────────────────────────────────────────┤
│  [ Start ]             [ Updater ]              [ Settings ]           │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│  - Start Page    : Live dual-server status, Launch and Stop controls,  │
│                    and 'Open ElectroERP App' launch button             │
│                    (each action guarded by 6-digit PIN confirmation).  │
│                                                                        │
│  - Updater Page  : Dedicated update cockpit (reserved for upcoming     │
│                    automated update delivery and patch rollouts).      │
│                                                                        │
│  - Settings Page : Responsive administrative layout:                   │
│                    • Row 1: Start with Windows & Auto Crash Recovery   │
│                    • Backend Location (full width as originally)       │
│                    • Frontend Location (full width as originally)      │
│                    • Bottom Row: Security PIN & Database Backup Status │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

1. **Start Page**:
   Primary operational dashboard displaying live dual-server status (Backend port 5000 + Frontend port 3000) with straightforward action buttons to launch and stop ElectroERP, and an `Open ElectroERP App` shortcut button when fully operational. Both start and stop operations require PIN verification.

2. **Updater Page**:
   Reserved workspace for upcoming software updating capabilities, planned to handle version checks and deployment.

3. **Settings Page**:
   Clean administrative configuration area for:
   - System Lifecycle: Start with Windows and Automatic Crash Recovery paired 2-column toggles.
   - Application Paths: Full-width rows for Node.js Backend Location and Next.js Frontend Location (retained as originally designed).
   - Security & Maintenance: Paired 2-column cards for 6-digit PIN authentication and Database Backup detection/action (data-only snapshots stored at `be_loc/../private/backup/`).

---

## Process Supervision & Windows Integration

- **System Tray Operation**: Closing the main application window minimizes it to the Windows System Tray rather than killing the server, allowing ElectroERP to run uninterrupted in the background.
- **Child Process Lifecycle Guarantee**: Binds backend (port 5000) and frontend (port 3000) processes to a Windows Job Object configured with `KillOnJobClose`, ensuring that whenever the Manager terminates, both servers and port allocations close immediately.
- **Automatic Crash Recovery**: Proactively monitors the server process and automatically restarts it if it crashes unexpectedly, minimizing store downtime.
- **Automatic Windows Startup**: Supports automatic launch upon Windows boot so the server is ready immediately without requiring manual operator intervention.

---

## Smart Error Tracking & Logging

ElectroERP Manager actively supervises the output streams of the ElectroERP server process:

- **Intelligent Error Detection**: Monitors standard output and error streams in real time to detect exceptions, stack traces, and runtime failures.
- **Automated Daily Rotation**: Persists identified error logs into structured, date-stamped log files located at:
  ```text
  logs\errors\errors-YYYY-MM-DD.log
  ```
- **Diagnostic Retention**: Ensures system administrators and developers have an organized historical log of server failures directly on the server machine for post-mortem analysis.

---

## Target Audience & Deployment Context

- **Host Environment**: Deployed locally on the dedicated server PC hosting ElectroERP.
- **Users**: Business administrators, store managers, and technicians who oversee the local ERP server infrastructure.
- **Website Engine Supervision (`WebsiteSupervisorService`)**: Manages the organization's **Premium Website Engine** (`electroerp-website`) on `http://localhost:3001` with Windows JobObject process binding, 6-digit PIN-guarded Launch/Stop Website controls, Open Website browser button, and configurable Website Location directory in Settings.
