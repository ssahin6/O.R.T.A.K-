<h1 align="center">O.R.T.A.K. Desktop Assistant 🤖</h1>

<div align="center">
  <p><strong>A locally-hosted, voice-controlled, AI-powered desktop assistant for Windows.</strong></p>
  
  ![C#](https://img.shields.io/badge/C%23-239120.svg?style=for-the-badge&logo=c-sharp&logoColor=white)
  ![WPF](https://img.shields.io/badge/WPF-512BD4.svg?style=for-the-badge&logo=windows&logoColor=white)
  ![Ollama](https://img.shields.io/badge/Ollama-FFFFFF.svg?style=for-the-badge&logo=ollama&logoColor=black)
</div>

<br />

<img width="1509" height="848" alt="Ekran görüntüsü 2026-10-06 015652" src="https://github.com/user-attachments/assets/d6c97ec6-8725-4100-9258-2ec3a3ac5508" />

## 📖 About The Project

O.R.T.A.K. is a custom-built, highly integrated desktop utility and AI assistant. It was designed to manage intense study sessions and minimize distractions in busy environments like dormitories. It acts as a digital thought partner for computer science coursework—whether you are tackling C programming practice or solving differential equations.

Unlike cloud-based voice assistants, O.R.T.A.K. prioritizes **100% privacy** by utilizing a locally hosted Large Language Model via Ollama. It bridges the gap between conversational AI and low-level system management, allowing you to control hardware, automate workflows, and manage media autonomously using native Windows APIs.

## ✨ Key Features

### 🧠 Local AI Engine
* **No Cloud Dependency:** Powered entirely by Ollama running the `qwen2.5:14b` model on your local machine.
* **Contextual Responses:** Acts as a polite and concise assistant for coding, research, and daily tasks.

### ⚙️ System Automation & Focus Modes
* **Coding Mode:** Instantly kills distracting background apps (like EA SPORTS FC 26, browsers, Discord), launches Visual Studio, and starts a focus playlist on Spotify.
* **Music Mode:** Clears the desktop of heavy applications and dedicates system resources to media playback.
* **Hardware Telemetry:** Real-time visual tracking of CPU load, RAM allocation, and SSD storage capacity.

### 🎵 Autonomous Media Control
* **Smart Silence Detection:** Uses `NAudio` to monitor system audio streams. If a browser starts playing audio, O.R.T.A.K. automatically intercepts and pauses Spotify via the Windows `user32.dll` API. Once the browser audio stops for 3 seconds, Spotify smoothly resumes.

### 📧 Secure Communications
* **Live IMAP Integration:** Fetches and displays unread emails securely from Gmail using the `MailKit` library.
* **Network Diagnostics:** Real-time ping monitoring and firewall status indicators.

---

## 🧰 Tech Stack & Tested Hardware

* **Language:** C#
* **UI Framework:** Windows Presentation Foundation (WPF)
* **AI Integration:** HTTP REST API (Ollama)
* **Audio Interception:** NAudio.CoreAudioApi
* **Tested On:** ASUS system with NVIDIA GeForce GTX 1650 and 40 GB RAM.

---

## 🚀 Installation & Configuration

### Prerequisites
1. **Windows 10/11**
2. **Visual Studio 2022** with the ".NET desktop development" workload.
3. **Ollama** installed and running.

### Setup Guide

1. **Clone the repository:**
   ```bash
   git clone [https://github.com/ssahin6/O.R.T.A.K.-.git](https://github.com/ssahin6/O.R.T.A.K.-.git)
   ```

2. **Initialize Local AI:**
   Pull the required LLM via terminal:
   ```bash
   ollama run qwen2.5:14b
   ```

3. **Configure Email Access:**
   * Generate a 16-character App Password from your Google Account settings.
   * Open `MainWindow.xaml.cs` and replace the credentials in the `MailleriKontrolEt()` method.

4. **Update Hardcoded Paths:**
   Open `OrtakCore.cs` and update the directory paths for applications like Epic Games or FC 26 to match your local installation.

---

## 🗣️ Voice Commands & Usage

**Always wake the assistant by saying `"ortak"` first.**

| Command (Spoken/Typed) | Action Triggered |
| :--- | :--- |
| `"ortak"` | Wakes up the voice sensor (UI changes to listening mode). |
| `"open coding mode"` | Kills distractions, launches Visual Studio and Spotify. |
| `"system report"` | Analyzes and vocalizes current SSD, CPU, and RAM status. |
| `"pause music"` | Manually hooks into Spotify to pause playback. |
| *[Any custom query]* | Forwards the query to the local AI model for an answer. |

---

## 📄 License

Distributed under the MIT License. Feel free to fork, modify, and use this codebase to build your own local desktop assistant!
