# PNL Indicator for cTrader

## Overview

This project is a custom indicator for the cTrader platform that displays real-time profit and loss (PNL) data directly on the chart.  
It is built to provide a **relaxed trading experience**, avoiding stress from rapidly changing monetary values by showing **pips instead of money**, and **smoothing updates with a timer**.

A [demo video](#demo) is available to see the indicator in action.

### Features

- ✅ Displays PNL grouped by symbol or by individual position.
- ✅ Presents data in pips instead of money for a more stable and calm display.
- ✅ Includes an update timer to avoid rapid flickering or noise.
- ✅ Fully customizable visual layout (fonts, colors, offsets, padding).
- ✅ Automatically detects new or closed positions.
- ✅ Lightweight on-screen panel integrated with the chart.

### Requirements

- A **cTrader** terminal.

### Getting Started

1. Open your **cTrader** terminal.
2. Create a **New Indicator**.
3. Copy the content of the `.cs` file into the editor.
4. Build and **attach the indicator to any chart**.

The script will:

1. Track open positions and volumes in real time.
2. Group positions by symbol into logical trades.
3. Initialize and manage a UI panel to display PNL.
4. Update the PNL view periodically using a timer (default every 1s).
5. Display changes in **pips** for less emotional bias compared to currency values.

### Demo

https://github.com/user-attachments/assets/1ab4e685-d348-4a44-b0a9-b22bd634210f

### License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### Contact

For questions or suggestions, please contact `bouchane.dev@gmail.com`.
