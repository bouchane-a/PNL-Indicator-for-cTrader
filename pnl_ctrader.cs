using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;


namespace cAlgo
{
    /*
     * Main indicator class ────────────────────────────────────────────────
     */
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class PNL : Indicator
    {
        // ──────────────────────────────────────────────────────────
        // ‖  State that triggers a UI refresh                    ‖
        // ──────────────────────────────────────────────────────────
        public double totalBuyVolume = 0.0;   // Sum of lots for all BUY positions
        public double totalSellVolume = 0.0;  // Sum of lots for all SELL positions

        // ──────────────────────────────────────────────────────────
        // ‖  Timer management                                     ‖
        // ──────────────────────────────────────────────────────────
        public int millisecondsTimer;         // Base timer period (ms)
        public int updateInTimerUnits;        // How many timer ticks equal one UI refresh
        public int updateCounter;             // Counts timer ticks since last refresh

        // ──────────────────────────────────────────────────────────
        // ‖  UI controls                                          ‖
        // ──────────────────────────────────────────────────────────
        public StackPanel pnlBackground;      // Container behind all TextBlocks
        public TextBlock[] pnlTextBlocks;     // One column per data field (symbol, type …)

        // ──────────────────────────────────────────────────────────
        // ‖  Public parameters (editable from cTrader)            ‖
        // ──────────────────────────────────────────────────────────
        [Parameter("Update In Seconds", DefaultValue = 1, MinValue = 0, MaxValue = 1000)]
        public int UpdateInSeconds { get; set; }

        [Parameter("Show Positions", DefaultValue = true)]
        public bool Show_Positions { get; set; }

        [Parameter("Padding", DefaultValue = 10, MinValue = 0, MaxValue = 200)]
        public int Padding { get; set; }

        [Parameter("X Distance", DefaultValue = 50, MinValue = 0, MaxValue = 1000)]
        public int X_Distance { get; set; }

        [Parameter("Y Distance", DefaultValue = 50, MinValue = 0, MaxValue = 1000)]
        public int Y_Distance { get; set; }

        [Parameter("X Offset", DefaultValue = 20, MinValue = 0, MaxValue = 1000)]
        public int X_Offset { get; set; }

        [Parameter("Trades Offset", DefaultValue = 1, MinValue = 0, MaxValue = 5)]
        public int Trades_Offset { get; set; }

        [Parameter("String Size", DefaultValue = 18, MinValue = 1, MaxValue = 200)]
        public int String_Size { get; set; }

        [Parameter("String Font", DefaultValue = "Tahoma")]
        public string String_Font { get; set; }

        [Parameter("String Color", DefaultValue = "White")]
        public Color String_Color { get; set; }

        [Parameter("Background Color", DefaultValue = "MidnightBlue")]
        public Color Background_Color { get; set; }

        [Parameter("Background Color Opacity", DefaultValue = 255, MinValue = 0, MaxValue = 255)]
        public int Background_Color_Opacity { get; set; }

        [Parameter("Pip Value Per Symbol", DefaultValue = "")]
        public string PipValuePerSymbol { get; set; }

        /*
         * Initialise indicator – sets up timer & UI.
         */
        protected override void Initialize()
        {
            // Make static helper class aware of this indicator instance
            A.algo = this;

            // Calculate how many 30 ms ticks equal <UpdateInSeconds>
            millisecondsTimer = 30;
            updateInTimerUnits = (int)(UpdateInSeconds * 1000 / millisecondsTimer);
            updateCounter = 0;

            // Store custom pip‐values (if any) in A.pipValues[]
            A.InitPipValues(PipValuePerSymbol);

            /*  Start timer and clear any previous chart objects – ensures the
             *  panel is redrawn cleanly after recompilation/hot reload. */
            Timer.Start(TimeSpan.FromMilliseconds(millisecondsTimer));
            Chart.RemoveAllObjects();

            // ────────── Build the TextBlock grid (one column per data field) ──────────
            pnlBackground = new StackPanel
            {
                BackgroundColor = Color.FromArgb(0, Background_Color), // Start transparent
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Orientation = Orientation.Horizontal,
                Margin = X_Distance.ToString()+" "+Y_Distance.ToString()+" 0 0"
            };

            // Create the required number of TextBlocks (see A.dataToDisplayCount)
            A.XArrayResize(ref pnlTextBlocks, A.dataToDisplayCount);

            // Left column gets left padding; middle columns spacing via X_Offset; last column right padding
            pnlTextBlocks[0] = ReturnTextBlock("", Padding, Padding, X_Offset, Padding);
            for (int i = 1; i < A.XArraySize(pnlTextBlocks) - 1; i++)
                pnlTextBlocks[i] = ReturnTextBlock("", 0, Padding, X_Offset, Padding);
            pnlTextBlocks[A.XArraySize(pnlTextBlocks) - 1] = ReturnTextBlock("", 0, Padding, Padding, Padding);

            // Add all TextBlocks into the StackPanel and onto the chart
            for (int i = 0; i < A.XArraySize(pnlTextBlocks); i++)
                pnlBackground.AddChild(pnlTextBlocks[i]);

            Chart.AddControl(pnlBackground);
        }

        /*
         * Called every <millisecondsTimer>. Refreshes panel only when
         *  – a new position is opened/closed (volumes change) OR
         *  – <UpdateInSeconds> has elapsed.
         */
        protected override void OnTimer()
        {
            // 1 - Detect position changes by comparing total volumes
            if (totalBuyVolume != A.TotalBuyVolume() || totalSellVolume != A.TotalSellVolume())
            {
                totalBuyVolume = A.TotalBuyVolume();
                totalSellVolume = A.TotalSellVolume();

                // Rebuild internal POSITION[] & TRADE[] lists
                ResetPNL();
                A.DeleteAll(ref A.positions, ref A.trades);

                /*  Cache a lightweight snapshot of current positions so we can
                 *  perform heavy calculations *only* inside our own objects. */
                foreach (Position position in A.algo.Positions)
                    A.AddPosition(ref A.positions, position.Id, (int)position.TradeType,
                                   position.Symbol.Name, position.Quantity, position.EntryPrice);

                // Group positions into logical trades (one per symbol)
                for (int i = 0; i < A.XArraySize(A.positions); i++)
                {
                    int symbolFound = A.PositionSymbolFound(A.trades, A.positions[i].symbol);
                    if (symbolFound == -1)
                    {
                        // Create new trade bucket
                        A.AddTrade(ref A.trades, A.positions[i].symbol);
                        A.trades[A.XArraySize(A.trades) - 1].AddToTrade(A.positions[i]);
                    }
                    else
                    {
                        // Append position to existing bucket
                        A.trades[symbolFound].AddToTrade(A.positions[i]);
                    }
                }

                UpdatePNL(); // Render fresh numbers

                // Fade‐in panel background only if there is content
                pnlBackground.BackgroundColor = Color.FromArgb(
                    String.Compare(pnlTextBlocks[0].Text, "") == 0 ? 0 : Background_Color_Opacity,
                    Background_Color);
            }

            // 2 - Periodic refresh even if nothing changed (slow UI heartbeat)
            updateCounter++;
            if (updateCounter == updateInTimerUnits)
            {
                updateCounter = 0;
                A.UpdateAll(A.positions, A.trades); // Re-calculate pips etc.
                UpdatePNL();                        // Update panel text
            }
        }

        // Not used – price-driven updates are intentionally ignored
        public override void Calculate(int index) { }

        /*
         * Clean-up when indicator is removed from the chart.
         */
        protected override void OnDestroy()
        {
            A.DeleteAll(ref A.positions, ref A.trades);
            DeletePNL();
            Chart.RemoveControl(pnlBackground);
            Chart.RemoveAllObjects();
        }

        /*
         * Utility – helper to create a TextBlock with consistent styling.
         */
        public TextBlock ReturnTextBlock(string _text, int _m1, int _m2, int _m3, int _m4)
        {
            return new TextBlock
            {
                Text = _text,
                ForegroundColor = String_Color,
                FontSize = String_Size,
                FontFamily = String_Font,
                Margin = $"{_m1} {_m2} {_m3} {_m4}"
            };
        }

        /*
         * Writes current POSITION[] and TRADE[] data into the TextBlocks.
         */
        public void UpdatePNL()
        {
            for (int i = 0; i < A.XArraySize(pnlTextBlocks); i++)
            {
                string pnlBlock = "";

                // ─── Individual positions ───
                if (Show_Positions)
                {
                    for (int x = 0; x < A.XArraySize(A.positions); x++)
                        pnlBlock += A.positions[x].data[i] + "\n";

                    // Visual gap between positions & trades
                    for (int k = 0; k < Trades_Offset; k++)
                        pnlBlock += "\n";
                }

                // ─── Aggregated trades ───
                for (int x = 0; x < A.XArraySize(A.trades); x++)
                    pnlBlock += A.trades[x].data[i] + "\n";

                pnlBlock=pnlBlock.Remove(pnlBlock.Length-1,1);
                pnlTextBlocks[i].Text = pnlBlock;
                
            }
        }

        // Convenience wrappers
        public void ResetPNL()  { for (int i = 0; i < A.XArraySize(pnlTextBlocks); i++) pnlTextBlocks[i].Text = ""; }
        public void DeletePNL() { ResetPNL(); A.XArrayResize(ref pnlTextBlocks, 0); }
    }

    /*
     * Lightweight snapshot of a single cTrader Position.
     */
    public class POSITION
    {
        // ─── Constants / meta data ───
        public int id;
        public int type;           // 0 = BUY, 1 = SELL
        public string typeString;  // Cached string to avoid ToString() in tight loops
        public string symbol;
        public string[] data;      // Pre-formatted strings for UI columns

        // ─── Calculated values ───
        public double volume;
        public double openPrice;
        public double closePrice;
        public double pips;
        public double pipvalue;

        public POSITION(int _id, int _type, string _symbol, double _volume, double _openPrice)
        {
            id = _id;
            type = _type;
            symbol = _symbol;
            volume = _volume;

            // Prepare UI data array (Symbol | Type | Lots | PipSize | PnL pips)
            A.XArrayResize(ref data, A.dataToDisplayCount);

            // Cache properties to avoid repeated look-ups
            openPrice = Math.Round(_openPrice, A.ReturnDigits(symbol));
            pipvalue = A.ReturnPipValue(symbol);
            typeString = type == 0 ? "BUY" : "SELL";

            UpdatePositionData();
        }

        /*
         * Pull latest price from cTrader and update cached metrics.
         */
        public void UpdatePositionData()
        {
            // Find the live position by ticket-id
            foreach (Position position in A.algo.Positions)
            {
                if (position.Id == id)
                {
                    closePrice = Math.Round(position.CurrentPrice, A.ReturnDigits(symbol));
                    pips = Math.Round((closePrice - openPrice) / pipvalue, 1) * Math.Pow(-1.0, (double)type);
                }
            }
            UpdateData();
        }

        // Refresh data[] strings for UI
        public void UpdateData()
        {
            data[0] = symbol;
            data[1] = typeString;
            data[2] = A.DoubleToString(volume, 2);
            data[3] = A.FormatPip(pipvalue);
            data[4] = A.DoubleToString(pips, 1);
        }
    }

    /*
     * Represents a group of positions on the same symbol (net trade).
     */
    public class TRADE
    {
        public POSITION[] tradePositions;

        // ─── Meta data ───
        public int positionsCount;
        public int type;           // 0 = NET LONG, 1 = NET SHORT, 3 = HEDGE/FLAT
        public string typeString;
        public string symbol;
        public string[] data;

        // ─── Aggregate values ───
        public double volume;
        public double volumeLong;
        public double volumeShort;
        public double openPrice;
        public double closePrice;
        public double pips;
        public double pipvalue;

        public TRADE(string _symbol)
        {
            symbol = _symbol;
            positionsCount = 0;
            A.XArrayResize(ref data, A.dataToDisplayCount);
        }

        /*
         * Add a POSITION snapshot to this trade bucket and immediately update
         * derived metrics (average price, net direction …).
         */
        public void AddToTrade(POSITION _position)
        {
            if (String.Compare(_position.symbol, symbol) == 0)
            {
                positionsCount++;
                A.XArrayResize(ref tradePositions, positionsCount);
                tradePositions[positionsCount - 1] = _position;
                InitTradeData();   // Calculates direction & avg price
                UpdateTradeData(); // Calculates live PnL
            }
        }

        /*
         * Derive static properties (direction, volume, average entry) from the
         * current basket of positions.
         */
        public void InitTradeData()
        {
            double totalVolume = 0.0;
            openPrice = 0.0;
            volumeLong = 0.0;
            volumeShort = 0.0;

            // Sum up component positions
            for (int y = 0; y < positionsCount; y++)
            {
                if (tradePositions[y].type == 0) volumeLong += tradePositions[y].volume;
                if (tradePositions[y].type == 1) volumeShort += tradePositions[y].volume;

                openPrice += tradePositions[y].volume * tradePositions[y].openPrice;
                totalVolume += tradePositions[y].volume;
            }

            volumeLong = Math.Round(volumeLong, 2);
            volumeShort = Math.Round(volumeShort, 2);

            // Determine net direction
            if (volumeLong > volumeShort) { type = 0; typeString = "BUY"; volume = Math.Round(volumeLong - volumeShort, 2); }
            else if (volumeLong < volumeShort) { type = 1; typeString = "SELL"; volume = Math.Round(volumeShort - volumeLong, 2); }
            else { type = 3; typeString = "HEDGE"; volume = volumeLong; }

            openPrice = Math.Round(openPrice / totalVolume, A.ReturnDigits(symbol));
        }

        /*
         * Update live PnL and close price estimate based on underlying
         * POSITION snapshots.
         */
        public void UpdateTradeData()
        {
            pips = 0.0;

            // Weighted average pips across all positions
            for (int y = 0; y < positionsCount; y++)
                pips += tradePositions[y].volume * tradePositions[y].pips;

            pips = Math.Round(pips / volume, 1);
            closePrice = Math.Round(openPrice + Math.Pow(-1.0, (double)type) * pips * pipvalue,
                                    A.ReturnDigits(symbol));

            UpdateData();
        }

        // Refresh data[] strings for UI
        public void UpdateData()
        {
            data[0] = symbol;
            data[1] = typeString;
            data[2] = A.DoubleToString(volume, 2);
            data[3] = A.DoubleToString(openPrice, A.ReturnDigits(symbol));
            data[4] = A.DoubleToString(pips, 1);
        }
    }

    /*
     * Static helper class – contains utility functions and singleton‐style
     * storage that needs to be shared across POSITION, TRADE & main indicator.
     */
    static class A
    {
        // Reference to the live indicator instance (set in Initialize)
        public static Algo algo;

        public static int dataToDisplayCount = 5; // #columns in UI

        // Optional per-symbol overrides (SYMBOL, pipSize, SYMBOL, pipSize …)
        public static string[] pipValues = new string[0];

        // Cached snapshots (updated via DeleteAll/AddPosition/AddTrade)
        public static POSITION[] positions = new POSITION[0];
        public static TRADE[] trades = new TRADE[0];

        // ──────────────────────────────────────────────────────────
        // ‖  Helper functions                                    ‖
        // ──────────────────────────────────────────────────────────
        public static double ReturnPipValue(string _symbol)
        {
            // Default: 10 × TickSize (works for most FX symbols)
            double pip = ReturnTickSize(_symbol) * 10.0;

            // Override if user supplied a manual mapping
            if (XArraySize(pipValues) > 0)
            {
                for (int i = 0; i < XArraySize(pipValues); i += 2)
                {
                    if (String.Compare(pipValues[i], _symbol) == 0 && i + 1 < XArraySize(pipValues))
                        pip = Double.Parse(pipValues[i + 1]);
                }
            }
            return pip;
        }

        public static void InitPipValues(string s)
        {
            XArrayResize(ref pipValues, 0);
            pipValues = s.Split(" ");
        }

        // Simple wrappers around cTrader Symbol API
        public static int ReturnDigits(string s)    => algo.Symbols.GetSymbol(s).Digits;
        public static double ReturnTickSize(string s) => algo.Symbols.GetSymbol(s).TickSize;

        // Trims trailing zeros for pip display
        public static string FormatPip(double p)
        {
            return p >= 1.0 ? DoubleToString(p, 1) : DoubleToString(p, 8)[..(DoubleToString(p, 8).IndexOf("1") + 1)];
        }

        public static string DoubleToString(double v, int d) => string.Format("{0:N" + Math.Abs(d) + "}", v);

        // ─── Position / Trade array helpers ───
        public static void AddPosition(ref POSITION[] array, int _ticket, int _type, string _symbol, double _volume, double _openPrice)
        {
            XArrayResize(ref array, XArraySize(array) + 1);
            array[^1] = new POSITION(_ticket, _type, _symbol, _volume, _openPrice);
        }

        public static void AddTrade(ref TRADE[] array, string _symbol)
        {
            XArrayResize(ref array, XArraySize(array) + 1);
            array[^1] = new TRADE(_symbol);
        }

        public static void UpdatePositions(POSITION[] array)
        {
            for (int x = 0; x < XArraySize(array); x++) array[x].UpdatePositionData();
        }
        public static void UpdateTrades(TRADE[] array)
        {
            for (int x = 0; x < XArraySize(array); x++) array[x].UpdateTradeData();
        }
        public static void UpdateAll(POSITION[] positions_array, TRADE[] trades_array)
        {
            UpdatePositions(positions_array);
            UpdateTrades(trades_array);
        }

        public static void DeletePositions(ref POSITION[] array) => XArrayResize(ref array, 0);
        public static void DeleteTrades(ref TRADE[] array)    => XArrayResize(ref array, 0);
        public static void DeleteAll(ref POSITION[] positions_array, ref TRADE[] trades_array)
        {
            DeletePositions(ref positions_array);
            DeleteTrades(ref trades_array);
        }

        /*
         * Returns index of trade bucket holding <_symbol> or -1 if not found.
         */
        public static int PositionSymbolFound(TRADE[] array, string _symbol)
        {
            for (int i = 0; i < XArraySize(array); i++)
                if (String.Compare(array[i].symbol, _symbol) == 0) return i;
            return -1;
        }

        // ─── Generic array resize helpers ───
        public static void XArrayResize(ref POSITION[] a, int s) => Array.Resize(ref a, s);
        public static void XArrayResize(ref TRADE[] a, int s)    => Array.Resize(ref a, s);
        public static void XArrayResize(ref string[] a, int s)   => Array.Resize(ref a, s);
        public static void XArrayResize(ref TextBlock[] a, int s)=> Array.Resize(ref a, s);

        // ─── Generic array size helpers ───
        public static int XArraySize(POSITION[] a) => a?.Length ?? 0;
        public static int XArraySize(TRADE[] a)    => a?.Length ?? 0;
        public static int XArraySize(string[] a)   => a?.Length ?? 0;
        public static int XArraySize(TextBlock[] a)=> a?.Length ?? 0;

        // ─── Volume helpers ───
        public static double TotalBuyVolume()
        {
            double v = 0.0;
            foreach (Position position in algo.Positions)
                if ((int)position.TradeType == 0) v += position.Quantity;
            return v;
        }
        public static double TotalSellVolume()
        {
            double v = 0.0;
            foreach (Position position in algo.Positions)
                if ((int)position.TradeType == 1) v += position.Quantity;
            return v;
        }
    }
}