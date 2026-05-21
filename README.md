# DiapStash Automation Hub (Plugin)

Welcome to the **DiapStash Automation Hub**! This application is a powerful, native Windows application built with WinUI 3. It serves as a unified bridge between your secure DiapStash cloud inventory, stream overlays (like OBS Studio), and TTS engines (like JakeyTTS). 

Whether you are tracking changes to your inventory in real-time or displaying rich, dynamic notifications on your livestream, this plugin handles the automation seamlessly.

## 🚀 Key Features

### 1. DiapStash Cloud Integration
- **OAuth2 Secure Authentication**: Connect securely to the DiapStash Developer Portal without exposing your root passwords.
- **Interactive Setup Wizard**: First-time users are guided through a sleek 4-step wizard to generate and apply API credentials.
- **Inventory Management**: View your entire DiapStash stock securely within the app using modern, fluent UI data grids.
- **Change Log Stream**: Monitor current change with all the current status. Define and create dynamic rules for specific items to be automatically displayed on stream triggered by chat commands or redeems.

### 2. Streaming Overlay Engine
- **OBS Studio Compatibility**: Features a customizable streaming overlay designed to be easily captured via an **OBS Browser Source**.
- **Drag & Drop Canvas Customization**: Fully customize your on-stream notifications! Add text labels, dynamically resize image containers, adjust border radiuses, and visually arrange elements freely on an interactive canvas.
- **Real-Time Data Binding**: Connect the overlay variables to DiapStash events so your viewers see high-quality, customized pop-ups whenever an event triggers.

### 3. JakeyTTS Bridge
- **Direct WebSocket Link**: Synchronizes automatically with a local JakeyTTS server instance (via `ws://localhost:8889/`).
- **Global Variable Passthrough**: Automatically pushes specific events and DiapStash updates into JakeyTTS as global variables, allowing you to trigger text-to-speech commands based on live inventory activity.

### 4. Modern Windows 11 Experience
- **WinUI 3 & Fluent Design**: Uses native Windows 11 design paradigms including Mica backdrops, rounded corners, and smooth micro-animations.
- **Light & Dark Mode**: Fully supports Windows system themes, including immersive dark-mode title bars.
- **Internationalization (I18N)**: Built with an extensible `.resw` resource framework, making it incredibly easy to translate the app into any language.

## ⚙️ Getting Started

1. **Launch the App**: On first boot, the app will automatically open the Setup Wizard.
2. **Link DiapStash**: Follow the wizard instructions to create an API Client in the DiapStash Developer Portal and paste your `Client ID` and `Client Secret`.
3. **Connect JakeyTTS** *(Optional)*: Head over to the Settings menu to define your local WebSocket endpoint and click "Start Loop Automation" to begin syncing TTS data.
4. **Configure your Overlay**: Open the "Streaming Overlay" tab, configure your UI elements, and capture the window in OBS to start streaming dynamic notifications!

## 🛠️ Architecture Notes

This project is built using:
- **C# & .NET 8.0**
- **WinUI 3 / Windows App SDK**
- **Google Material Symbols** (For precise vector navigation icons)
- **Local HttpListener** (For handling secure OAuth2 loopback redirects)

---

*Automate your stream. Track your inventory. Entertain your viewers.*