# AzerothCoreManager

AzerothCoreManager is a lightweight Windows application for managing **AzerothCore servers**.  
It allows starting and stopping the Authserver, Worldserver, and SQL Server, while also providing live console output and resource monitoring.
This is a beginner project to learn **C#** and **WPF**.

---

## **Current Features**

1. **Start and Stop Servers**
   - Authserver (`authserver.exe`)
   - Worldserver (`worldserver.exe`)
   - SQL Server (via Windows Service, e.g., MySQL)
   - The application automatically detects running servers and updates the UI accordingly.

2. **Live Console Output**
   - Logs from Authserver and Worldserver are displayed in real-time within the application.
   - Allows easy monitoring and troubleshooting of server activity.

3. **Server Path Management**
   - Stores the path to server binaries in a `Settings.txt` file.
   - The user is prompted to set the folder containing `authserver.exe` and `worldserver.exe` if no path is saved.
   - Start/Stop buttons remain disabled until a valid path is set.

4. **Resource Monitoring**
   - Displays CPU and RAM usage of Authserver and Worldserver.
   - Comparison against total system resource usage.

5. **SQL Service Monitoring**
   - Shows the status of the MySQL service.
   - Start, Stop, and Restart MySQL directly from the UI.

---

## **Current known Bugs**

   - Currently the Authserver Log doesn't show anything from the authserver.exe

---

## **About Me**

I am a beginner in **C#** and **WPF**, and I wanted to combine two things I enjoy:  
managing an AzerothCore server and creating graphical user interfaces.  

Because this is an ongoing learning project, the application may have some rough edges. But I will try to improve it over time! 
Feedback and suggestions on how to structure the application, improve functionality, or tackle bigger challenges are highly appreciated.

---

## **Planned Features**

- Management of AzerothCore source code
- Checking and Installing Prerequisites
- Module management
- Building the server from source
- Realm and user management
- Additional tools to simplify server administration
- More advanced logging and monitoring options

---

## **Requirements**

- Existing AzerothCore server installation
- Windows 10 or higher
- .NET 9.0 Runtime installed
- MySQL Windows Service for SQL Server

---

## **Getting Started**

1. Launch `AzerothCoreManager.exe`.
2. On the first run, if no `Settings.txt` exists, select the folder containing `authserver.exe` and `worldserver.exe`.
3. Once the path is set, Start/Stop buttons are enabled.
4. Start the servers as needed, monitor logs, and watch resource usage.
