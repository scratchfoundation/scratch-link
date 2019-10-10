// This sample is provided "AS IS" and confers no warranties.
// You are granted a non-exclusive, worldwide, royalty-free license to reproduce this code,
// prepare derivative works, and distribute it or any derivative works that you create.
//
// This class invokes the Windows IPHelper APIs that allow us to map sockets to processes.
// See http://www.pinvoke.net/default.aspx/iphlpapi/GetExtendedTcpTable.html as a reference
//
// We could consider a cache of recent hits to improve performance, but the performance is already pretty good, and 
// creating a reasonable cache expiration policy could prove tricky. Client connection reuse already provides a significant
// optimization as it behaves in the same way as an explicit cache would.
// 
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Fiddler
{
    internal class Winsock
    {
        #region IPHelper_PInvokes

        private const int AF_INET = 2;              // IPv4
        private const int AF_INET6 = 23;            // IPv6
        private const int ERROR_INSUFFICIENT_BUFFER = 0x7a;
        private const int NO_ERROR = 0x0;

        // Learn about IPHelper here: http://msdn2.microsoft.com/en-us/library/aa366073.aspx and http://msdn2.microsoft.com/en-us/library/aa365928.aspx
        // Note: C++'s ulong is ALWAYS 32bits, unlike C#'s ulong. See http://medo64.blogspot.com/2009/05/why-ulong-is-32-bit-even-on-64-bit.html
        [DllImport("iphlpapi.dll", ExactSpelling = true, SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref UInt32 dwTcpTableLength, [MarshalAs(UnmanagedType.Bool)] bool sort, UInt32 ipVersion, TcpTableType tcpTableType, UInt32 reserved);

        /// <summary>
        /// Enumeration of possible queries that can be issued using GetExtendedTcpTable
        /// http://msdn2.microsoft.com/en-us/library/aa366386.aspx
        /// </summary>
        private enum TcpTableType
        {
            BasicListener,
            BasicConnections,
            BasicAll,
            OwnerPidListener,
            OwnerPidConnections,
            OwnerPidAll,
            OwnerModuleListener,
            OwnerModuleConnections,
            OwnerModuleAll
        }

/* This code is now obsolete as I'm now using pointer-arithmetic to directly access the table rows instead of mapping structs on top of the 
 * returned block of data. I'm keeping the code here for now for debugging purposes.*/
        // http://msdn2.microsoft.com/en-us/library/aa366913.aspx
        [StructLayout(LayoutKind.Sequential)]
        private struct TcpRow
        {
            [MarshalAs(UnmanagedType.U4)]
            internal UInt32 tcpState;
            [MarshalAs(UnmanagedType.U4)]
            internal UInt32 localAddr;
            [MarshalAs(UnmanagedType.U4)]
            internal UInt32 localPortInNetworkOrder;
            [MarshalAs(UnmanagedType.U4)]
            internal UInt32 remoteAddr;
            [MarshalAs(UnmanagedType.U4)]
            internal UInt32 remotePortInNetworkOrder;
            [MarshalAs(UnmanagedType.U4)]
            internal Int32 owningPid;
        }
        private static string TcpRowToString(TcpRow rowInput)
        {
            return String.Format(">{0}:{1} to {2}:{3} is {4} by 0x{5:x}",
                (rowInput.localAddr & 0xFF) + "." + ((rowInput.localAddr & 0xFF00) >> 8) + "." + ((rowInput.localAddr & 0xFF0000) >> 16) + "." + ((rowInput.localAddr & 0xFF000000) >> 24),
                ((rowInput.localPortInNetworkOrder & 0xFF00) >> 8) + ((rowInput.localPortInNetworkOrder & 0xFF) << 8),
                (rowInput.remoteAddr & 0xFF) + "." + ((rowInput.remoteAddr & 0xFF00) >> 8) + "." + ((rowInput.remoteAddr & 0xFF0000) >> 16) + "." + ((rowInput.remoteAddr & 0xFF000000) >> 24),
                ((rowInput.remotePortInNetworkOrder & 0xFF00) >> 8) + ((rowInput.remotePortInNetworkOrder & 0xFF) << 8),
                rowInput.tcpState,
                rowInput.owningPid);
        }

        #endregion IPHelper_PInvokes

        /// <summary>
        /// Map a local port number to the originating process ID
        /// </summary>
        /// <param name="iPort">The local port number</param>
        /// <returns>The originating process ID</returns>
        internal static int MapLocalPortToProcessId(int iPort)
        {
            Debug.Assert(((iPort > 0) && (iPort < 65536)), "Unexpected client port value");
            // Stopwatch oSW = Stopwatch.StartNew();
            int result = FindPIDForPort(iPort);
            // FiddlerApplication.Log.LogString("Port hunt took: " + oSW.ElapsedMilliseconds); // Current version seems to take about 1ms on average, with a range up to ~35ms.
            return result;
        }
      
        /// <summary>
        /// Calls the GetExtendedTcpTable function to map a port to a process ID.
        /// This function is (over) optimized for performance.
        /// </summary>
        /// <param name="iTargetPort">Client port</param>
        /// <param name="iAddressType">AF_INET or AF_INET6</param>
        /// <returns>PID, if found, or 0</returns>
        private static int FindPIDForConnection(int iTargetPort, uint iAddressType)
        {
            Debug.Assert(iAddressType == AF_INET6 || iAddressType == AF_INET);
            IntPtr ptrTcpTable = IntPtr.Zero;
            UInt32 tcpTableLength = 0;

            int iOffsetToFirstPort = 12;
            int iOffsetToPIDInRow = 12;
            int iTableRowSize = 24; // 24 == Marshal.SizeOf(typeof(TcpRow));

            // IPv6 tables are a different size, so adjust the offsets accordingly
            if (iAddressType == AF_INET6)
            {
                iOffsetToFirstPort = 24;
                iOffsetToPIDInRow = 32;
                iTableRowSize = 56;
            }

            // Determine the size of the memory block to allocate
            if (ERROR_INSUFFICIENT_BUFFER == GetExtendedTcpTable(ptrTcpTable, ref tcpTableLength, false, iAddressType, TcpTableType.OwnerPidAll, 0))
            {
                try
                {
                    ptrTcpTable = Marshal.AllocHGlobal((Int32)tcpTableLength);

                    // Would it be faster to set the SORTED argument to true, and then iterate the table in reverse order?
                    if (NO_ERROR == GetExtendedTcpTable(ptrTcpTable, ref tcpTableLength, false, iAddressType, TcpTableType.OwnerPidAll, 0))
                    {
                        // Convert port we're looking for into Network byte order
                        int iTargetPortInNetOrder = ((iTargetPort & 0xFF) << 8) + ((iTargetPort & 0xFF00) >> 8);

                        // ISSUE: This function APPEARS to work fine, but might blow up on Itanium or exotic architectures like that. As noted in the docs:
                        // The MIB_TCPTABLE_OWNER_PID structure may contain padding for alignment between the dwNumEntries member and the first MIB_TCPROW_OWNER_PID
                        // array entry in the table  member. Padding for alignment may also be present between the MIB_TCPROW_OWNER_PID array entries in the table member. 
                        // Any access to a MIB_TCPROW_OWNER_PID array entry should assume padding may exist. 
                        //
                        // I have absolutely no idea how to detect such padding, or if .NET handles it automatically if I use PtrToStructure rather than the direct pointer 
                        // manipulation calls this function is now using.
                        //
                        int tableLen = Marshal.ReadInt32(ptrTcpTable);          // Get table row count
                        if (tableLen == 0)
                        {
                            Debug.Assert(false, "How is it possible that the API succeeded and there are really no network connections? Maybe pure IPv6 environment?");
                            return 0;
                        }
                        IntPtr ptrRow = (IntPtr)((long)ptrTcpTable + iOffsetToFirstPort);       // Advance pointer to first Port in the table

                        // Iterate each row of the table, looking to see if localPortInNetworkOrder matches. If it does, return the owningPid
                        for (int i = 0; i < tableLen; ++i)
                        {
                            // Check for matching local port
                            if (iTargetPortInNetOrder == Marshal.ReadInt32(ptrRow))
                            {
                                return Marshal.ReadInt32(ptrRow, iOffsetToPIDInRow);    
                                // Note: the finally clause below will clean up memory
                            }

                            // Move to the next row
                            ptrRow = (IntPtr)((long)ptrRow + iTableRowSize);
                        }
                    }
                    else
                    {
                        throw new Exception(string.Format("GetExtendedTcpTable() returned error #{0}", Marshal.GetLastWin32Error().ToString()));
                    }
                }
                finally
                {
                    // Clean up unmanaged memory block. Call succeeds even if tcpTable == 0.
                    Marshal.FreeHGlobal(ptrTcpTable);
                }
            }
            else
            {
                throw new Exception(string.Format("Initial call to GetExtendedTcpTable() returned error #{0}", Marshal.GetLastWin32Error().ToString()));
            }
            return 0;
        }

        /// <summary>
        /// Given a local port number, uses GetExtendedTcpTable to find the originating process ID. 
        /// First checks the IPv4 connections, then looks at IPv6 connections
        /// </summary>
        /// <param name="iTargetPort">Client applications' port</param>
        /// <returns>ProcessID, or 0 if not found</returns>
        private static int FindPIDForPort(int iTargetPort)
        {          
            int iPID = 0;
            try
            {
                const bool bEnableIPv6 = true;
                iPID = FindPIDForConnection(iTargetPort, AF_INET);
                if ((iPID > 0) || !bEnableIPv6) return iPID;
                return FindPIDForConnection(iTargetPort, AF_INET6);
            }
            catch (Exception eX)
            {
                //FiddlerApplication.Log.LogFormat("Fiddler.Network.TCPTable> Unable to call IPHelperAPI function: {0}", eX.Message);
                Debug.Assert(false, "Unable to call IPHelperAPI function" + eX.Message);
            }

            // If we got here, we didn't find the connection; this will occur if the connection is from a remote client.
            // FiddlerApplication.Log.LogFormat("Fiddler.Network.TCPTable.Error> Unable to find process information for port #{0} in table of length {1}", iTargetPort, tcpTableLength);
            return 0;
        }
    }
}
