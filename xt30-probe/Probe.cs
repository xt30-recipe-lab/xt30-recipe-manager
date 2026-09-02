// ============================================================================
// xt30-probe v0.1.0 — Sonde PTP EN LECTURE SEULE pour Fujifilm X-T30 (gen 1)
//
// SECURITE : cet outil ne peut PAS écrire dans l'appareil.
//   - Seuls trois opcodes PTP sont autorisés, tous en lecture :
//       0x1001 GetDeviceInfo
//       0x1014 GetDevicePropDesc
//       0x1015 GetDevicePropValue
//   - Le garde-fou est un point de passage unique (MtpReadOnlyGuard.Check)
//     appelé avant CHAQUE envoi de commande. SetDevicePropValue (0x1016),
//     les opcodes vendor (0x9xxx) et toute autre opération sont refusés.
//   - Transport : API Windows WPD (Windows Portable Devices) + passthrough
//     MTP officiel de Microsoft. Aucun driver n'est modifié.
//
// Sortie : console + xt30_report.json + xt30_report.txt
//
// Compilation (compilateur C# intégré à Windows, aucun SDK requis) :
//   %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /out:xt30-probe.exe Probe.cs
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Xt30Probe
{
    // ------------------------------------------------------------------
    // Structures COM de base
    // ------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
        public PropertyKey(Guid f, uint p) { fmtid = f; pid = p; }
        public PropertyKey(string f, uint p) { fmtid = new Guid(f); pid = p; }
    }

    // PROPVARIANT simplifié (16 octets x86 / 24 octets x64)
    [StructLayout(LayoutKind.Sequential)]
    public struct PropVariant
    {
        public ushort vt;
        public ushort r1;
        public ushort r2;
        public ushort r3;
        public IntPtr p;
        public IntPtr p2;

        public const ushort VT_UI4 = 19;

        public static PropVariant FromUInt32(uint v)
        {
            PropVariant pv = new PropVariant();
            pv.vt = VT_UI4;
            pv.p = new IntPtr(unchecked((int)v));
            return pv;
        }

        public uint AsUInt32()
        {
            return unchecked((uint)(p.ToInt64() & 0xFFFFFFFFL));
        }
    }

    // ------------------------------------------------------------------
    // Interfaces WPD (ordre vtable vérifié dans PortableDeviceApi.h /
    // PortableDeviceTypes.h du SDK Windows 10 — copies dans docs/reference)
    // ------------------------------------------------------------------

    [ComImport, Guid("a1567595-4c2f-4574-a6fa-ecef917b9a40"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPortableDeviceManager
    {
        // MarshalAs LPArray obligatoire : sans lui, .NET marshale en SAFEARRAY
        // et l'API WPD (qui ecrit des LPWSTR bruts) corrompt le tas -> crash differe.
        void GetDevices([In, Out, MarshalAs(UnmanagedType.LPArray)] IntPtr[] pPnPDeviceIDs, ref uint pcPnPDeviceIDs);
        void RefreshDeviceList();
        void GetDeviceFriendlyName([MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID,
            [In, Out, MarshalAs(UnmanagedType.LPArray)] ushort[] pDeviceFriendlyName, ref uint pcchDeviceFriendlyName);
        void GetDeviceDescription([MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID,
            [In, Out, MarshalAs(UnmanagedType.LPArray)] ushort[] pDeviceDescription, ref uint pcchDeviceDescription);
        void GetDeviceManufacturer([MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID,
            [In, Out, MarshalAs(UnmanagedType.LPArray)] ushort[] pDeviceManufacturer, ref uint pcchDeviceManufacturer);
        void GetDeviceProperty_Unused(IntPtr a, IntPtr b, IntPtr c, IntPtr d, IntPtr e); // non utilisé
        void GetPrivateDevices([In, Out, MarshalAs(UnmanagedType.LPArray)] IntPtr[] pPnPDeviceIDs, ref uint pcPnPDeviceIDs);
    }

    [ComImport, Guid("625e2df8-6392-4cf0-9ad1-3cfa5f17775c"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPortableDevice
    {
        void Open([MarshalAs(UnmanagedType.LPWStr)] string pszPnPDeviceID, IPortableDeviceValues pClientInfo);
        void SendCommand(uint dwFlags, IPortableDeviceValues pParameters, out IPortableDeviceValues ppResults);
        void Content_Unused(out IntPtr pp);      // non utilisé
        void Capabilities_Unused(out IntPtr pp); // non utilisé
        void Cancel();
        void Close();
        void Advise_Unused(uint a, IntPtr b, IntPtr c, out IntPtr d);   // non utilisé
        void Unadvise_Unused(IntPtr a);                                  // non utilisé
        void GetPnPDeviceID_Unused(out IntPtr pp);                       // non utilisé
    }

    [ComImport, Guid("6848f6f2-3155-4f86-b6f5-263eeeab3143"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPortableDeviceValues
    {
        void GetCount(ref uint pcelt);
        void GetAt(uint index, ref PropertyKey pKey, ref PropVariant pValue);
        void SetValue(ref PropertyKey key, ref PropVariant pValue);
        void GetValue(ref PropertyKey key, out PropVariant pValue);
        void SetStringValue(ref PropertyKey key, [MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetStringValue(ref PropertyKey key, [MarshalAs(UnmanagedType.LPWStr)] out string pValue);
        void SetUnsignedIntegerValue(ref PropertyKey key, uint value);
        void GetUnsignedIntegerValue(ref PropertyKey key, out uint pValue);
        void SetSignedIntegerValue(ref PropertyKey key, int value);
        void GetSignedIntegerValue(ref PropertyKey key, out int pValue);
        void SetUnsignedLargeIntegerValue(ref PropertyKey key, ulong value);
        void GetUnsignedLargeIntegerValue(ref PropertyKey key, out ulong pValue);
        void SetSignedLargeIntegerValue(ref PropertyKey key, long value);
        void GetSignedLargeIntegerValue(ref PropertyKey key, out long pValue);
        void SetFloatValue(ref PropertyKey key, float value);
        void GetFloatValue(ref PropertyKey key, out float pValue);
        void SetErrorValue(ref PropertyKey key, int value);
        void GetErrorValue(ref PropertyKey key, out int pValue);
        void SetKeyValue(ref PropertyKey key, ref PropertyKey value);
        void GetKeyValue(ref PropertyKey key, out PropertyKey pValue);
        void SetBoolValue(ref PropertyKey key, int value);
        void GetBoolValue(ref PropertyKey key, out int pValue);
        void SetIUnknownValue(ref PropertyKey key, [MarshalAs(UnmanagedType.IUnknown)] object pValue);
        void GetIUnknownValue(ref PropertyKey key, [MarshalAs(UnmanagedType.IUnknown)] out object ppValue);
        void SetGuidValue(ref PropertyKey key, ref Guid value);
        void GetGuidValue(ref PropertyKey key, out Guid pValue);
        void SetBufferValue(ref PropertyKey key,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pValue, uint cbValue);
        void GetBufferValue(ref PropertyKey key, out IntPtr ppValue, out uint pcbValue);
        void SetIPortableDeviceValuesValue(ref PropertyKey key, IPortableDeviceValues pValue);
        void GetIPortableDeviceValuesValue(ref PropertyKey key, out IPortableDeviceValues ppValue);
        void SetIPortableDevicePropVariantCollectionValue(ref PropertyKey key, IPortableDevicePropVariantCollection pValue);
        void GetIPortableDevicePropVariantCollectionValue(ref PropertyKey key, out IPortableDevicePropVariantCollection ppValue);
        void SetIPortableDeviceKeyCollectionValue_Unused(ref PropertyKey key, IntPtr pValue);   // non utilisé
        void GetIPortableDeviceKeyCollectionValue_Unused(ref PropertyKey key, out IntPtr pp);   // non utilisé
        void SetIPortableDeviceValuesCollectionValue_Unused(ref PropertyKey key, IntPtr pValue);// non utilisé
        void GetIPortableDeviceValuesCollectionValue_Unused(ref PropertyKey key, out IntPtr pp);// non utilisé
        void RemoveValue(ref PropertyKey key);
        void CopyValuesFromPropertyStore_Unused(IntPtr p);  // non utilisé
        void CopyValuesToPropertyStore_Unused(IntPtr p);    // non utilisé
        void Clear();
    }

    [ComImport, Guid("89b2e422-4f1b-4316-bcef-a44afea83eb3"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPortableDevicePropVariantCollection
    {
        void GetCount(ref uint pcElems);
        void GetAt(uint dwIndex, ref PropVariant pValue);
        void Add(ref PropVariant pValue);
        void GetType(out ushort pvt);
        void ChangeType(ushort vt);
        void Clear();
        void RemoveAt(uint dwIndex);
    }

    // ------------------------------------------------------------------
    // Constantes WPD (vérifiées dans PortableDevice.h / WpdMtpExtensions.h)
    // ------------------------------------------------------------------

    public static class Wpd
    {
        public static readonly Guid CLSID_PortableDeviceManager = new Guid("0af10cec-2ecd-4b92-9581-34f6ae0637f3");
        public static readonly Guid CLSID_PortableDeviceFTM = new Guid("f7c0039a-4762-488a-b4b3-760ef9a1ba9b");
        public static readonly Guid CLSID_PortableDevice = new Guid("728a21c5-3d9e-48d7-9810-864848f0f404");
        public static readonly Guid CLSID_PortableDeviceValues = new Guid("0c15d503-d017-47ce-9016-7b3f978721cc");
        public static readonly Guid CLSID_PortableDevicePropVariantCollection = new Guid("08a99e2f-6d6d-4b80-af5a-baf2bcbe4cb9");

        const string ClientInfoGuid = "204D9F0C-2292-4080-9F42-40664E70F859";
        public static PropertyKey WPD_CLIENT_NAME = new PropertyKey(ClientInfoGuid, 2);
        public static PropertyKey WPD_CLIENT_MAJOR_VERSION = new PropertyKey(ClientInfoGuid, 3);
        public static PropertyKey WPD_CLIENT_MINOR_VERSION = new PropertyKey(ClientInfoGuid, 4);
        public static PropertyKey WPD_CLIENT_REVISION = new PropertyKey(ClientInfoGuid, 5);
        public static PropertyKey WPD_CLIENT_SECURITY_QUALITY_OF_SERVICE = new PropertyKey(ClientInfoGuid, 8);

        const string CommonGuid = "F0422A9C-5DC8-4440-B5BD-5DF28835658A";
        public static PropertyKey WPD_PROPERTY_COMMON_COMMAND_CATEGORY = new PropertyKey(CommonGuid, 1001);
        public static PropertyKey WPD_PROPERTY_COMMON_COMMAND_ID = new PropertyKey(CommonGuid, 1002);
        public static PropertyKey WPD_PROPERTY_COMMON_HRESULT = new PropertyKey(CommonGuid, 1003);
        public static PropertyKey WPD_PROPERTY_COMMON_DRIVER_ERROR_CODE = new PropertyKey(CommonGuid, 1004);

        public const string MtpExtGuid = "4D545058-1A2E-4106-A357-771E0819FC56";
        public static readonly Guid WPD_CATEGORY_MTP_EXT = new Guid(MtpExtGuid);
        public static PropertyKey CMD_GET_SUPPORTED_VENDOR_OPCODES = new PropertyKey(MtpExtGuid, 11);
        public static PropertyKey CMD_EXECUTE_WITHOUT_DATA_PHASE = new PropertyKey(MtpExtGuid, 12);
        public static PropertyKey CMD_EXECUTE_WITH_DATA_TO_READ = new PropertyKey(MtpExtGuid, 13);
        public static PropertyKey CMD_READ_DATA = new PropertyKey(MtpExtGuid, 15);
        public static PropertyKey CMD_END_DATA_TRANSFER = new PropertyKey(MtpExtGuid, 17);
        public static PropertyKey PROP_OPERATION_CODE = new PropertyKey(MtpExtGuid, 1001);
        public static PropertyKey PROP_OPERATION_PARAMS = new PropertyKey(MtpExtGuid, 1002);
        public static PropertyKey PROP_RESPONSE_CODE = new PropertyKey(MtpExtGuid, 1003);
        public static PropertyKey PROP_RESPONSE_PARAMS = new PropertyKey(MtpExtGuid, 1004);
        public static PropertyKey PROP_VENDOR_OPERATION_CODES = new PropertyKey(MtpExtGuid, 1005);
        public static PropertyKey PROP_TRANSFER_CONTEXT = new PropertyKey(MtpExtGuid, 1006);
        public static PropertyKey PROP_TRANSFER_TOTAL_DATA_SIZE = new PropertyKey(MtpExtGuid, 1007);
        public static PropertyKey PROP_TRANSFER_NUM_BYTES_TO_READ = new PropertyKey(MtpExtGuid, 1008);
        public static PropertyKey PROP_TRANSFER_NUM_BYTES_READ = new PropertyKey(MtpExtGuid, 1009);
        public static PropertyKey PROP_TRANSFER_DATA = new PropertyKey(MtpExtGuid, 1012);
        public static PropertyKey PROP_OPTIMAL_TRANSFER_BUFFER_SIZE = new PropertyKey(MtpExtGuid, 1013);

        public static IPortableDeviceValues CreateValues()
        {
            return (IPortableDeviceValues)Activator.CreateInstance(
                Type.GetTypeFromCLSID(CLSID_PortableDeviceValues));
        }

        public static IPortableDevicePropVariantCollection CreatePropVariantCollection()
        {
            return (IPortableDevicePropVariantCollection)Activator.CreateInstance(
                Type.GetTypeFromCLSID(CLSID_PortableDevicePropVariantCollection));
        }
    }

    // ------------------------------------------------------------------
    // GARDE-FOU LECTURE SEULE — point de passage unique et non contournable
    // ------------------------------------------------------------------

    public static class MtpReadOnlyGuard
    {
        // Opcodes PTP autorisés : LECTURE UNIQUEMENT.
        // 0x1016 SetDevicePropValue et tout opcode vendor sont exclus par principe.
        static readonly ushort[] Allowed = new ushort[] {
            0x1001, // GetDeviceInfo
            0x1004, // GetStorageIDs
            0x1005, // GetStorageInfo
            0x1007, // GetObjectHandles
            0x1008, // GetObjectInfo
            // 0x1009 GetObject : lecture pure appareil -> PC, autorisee explicitement par
            // l'utilisateur le 2026-09-02 pour lire le fichier de reglages (handle 0,
            // format 0x5000). Aucune donnee n'est envoyee au boitier. Seul
            // Tools/BackupRead l'utilise, et il la restreint au handle 0.
            0x1009, // GetObject
            0x1014, // GetDevicePropDesc
            0x1015  // GetDevicePropValue
        };

        public static void Check(ushort opcode)
        {
            for (int i = 0; i < Allowed.Length; i++)
                if (Allowed[i] == opcode) return;
            throw new InvalidOperationException(string.Format(
                "GARDE-FOU READ-ONLY : opcode PTP 0x{0:X4} refusé. " +
                "Seules les lectures PTP explicitement listées sont autorisées.", opcode));
        }
    }

    // ------------------------------------------------------------------
    // Client MTP passthrough (lecture uniquement)
    // ------------------------------------------------------------------

    public class MtpDevice : IDisposable
    {
        IPortableDevice _device;
        public string PnpId;

        [DllImport("ole32.dll")]
        static extern int PropVariantClear(ref PropVariant pvar);

        public static List<string> ListDeviceIds()
        {
            IPortableDeviceManager mgr = (IPortableDeviceManager)Activator.CreateInstance(
                Type.GetTypeFromCLSID(Wpd.CLSID_PortableDeviceManager));
            mgr.RefreshDeviceList();
            uint count = 0;
            mgr.GetDevices(null, ref count);
            List<string> ids = new List<string>();
            if (count > 0)
            {
                IntPtr[] ptrs = new IntPtr[count];
                mgr.GetDevices(ptrs, ref count);
                for (uint i = 0; i < count; i++)
                {
                    if (ptrs[i] != IntPtr.Zero)
                    {
                        ids.Add(Marshal.PtrToStringUni(ptrs[i]));
                        Marshal.FreeCoTaskMem(ptrs[i]);
                    }
                }
            }
            Marshal.ReleaseComObject(mgr);
            return ids;
        }

        public static string GetDeviceString(string pnpId, int which)
        {
            IPortableDeviceManager mgr = (IPortableDeviceManager)Activator.CreateInstance(
                Type.GetTypeFromCLSID(Wpd.CLSID_PortableDeviceManager));
            try
            {
                uint cch = 0;
                if (which == 0) mgr.GetDeviceFriendlyName(pnpId, null, ref cch);
                else if (which == 1) mgr.GetDeviceManufacturer(pnpId, null, ref cch);
                else mgr.GetDeviceDescription(pnpId, null, ref cch);
                if (cch == 0) return "";
                ushort[] buf = new ushort[cch];
                if (which == 0) mgr.GetDeviceFriendlyName(pnpId, buf, ref cch);
                else if (which == 1) mgr.GetDeviceManufacturer(pnpId, buf, ref cch);
                else mgr.GetDeviceDescription(pnpId, buf, ref cch);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < buf.Length && buf[i] != 0; i++) sb.Append((char)buf[i]);
                return sb.ToString();
            }
            catch (Exception) { return ""; }
            finally { Marshal.ReleaseComObject(mgr); }
        }

        public void Open(string pnpId)
        {
            PnpId = pnpId;
            IPortableDeviceValues clientInfo = Wpd.CreateValues();
            clientInfo.SetStringValue(ref Wpd.WPD_CLIENT_NAME, "xt30-probe (read-only)");
            clientInfo.SetUnsignedIntegerValue(ref Wpd.WPD_CLIENT_MAJOR_VERSION, 0);
            clientInfo.SetUnsignedIntegerValue(ref Wpd.WPD_CLIENT_MINOR_VERSION, 1);
            clientInfo.SetUnsignedIntegerValue(ref Wpd.WPD_CLIENT_REVISION, 0);
            // SECURITY_IMPERSONATION (SecurityImpersonation << 16)
            clientInfo.SetUnsignedIntegerValue(ref Wpd.WPD_CLIENT_SECURITY_QUALITY_OF_SERVICE, 0x20000);

            IPortableDevice dev;
            try
            {
                dev = (IPortableDevice)Activator.CreateInstance(Type.GetTypeFromCLSID(Wpd.CLSID_PortableDeviceFTM));
            }
            catch (Exception)
            {
                dev = (IPortableDevice)Activator.CreateInstance(Type.GetTypeFromCLSID(Wpd.CLSID_PortableDevice));
            }
            dev.Open(pnpId, clientInfo);
            _device = dev;
        }

        IPortableDeviceValues Send(IPortableDeviceValues parameters)
        {
            IPortableDeviceValues results;
            _device.SendCommand(0, parameters, out results);
            int hr = 0;
            try { results.GetErrorValue(ref Wpd.WPD_PROPERTY_COMMON_HRESULT, out hr); }
            catch (Exception) { hr = 0; }
            if (hr < 0)
                throw new COMException(string.Format("La commande WPD a échoué (HRESULT 0x{0:X8})", hr), hr);
            return results;
        }

        IPortableDeviceValues BuildCommand(PropertyKey cmd)
        {
            IPortableDeviceValues p = Wpd.CreateValues();
            Guid cat = cmd.fmtid;
            p.SetGuidValue(ref Wpd.WPD_PROPERTY_COMMON_COMMAND_CATEGORY, ref cat);
            p.SetUnsignedIntegerValue(ref Wpd.WPD_PROPERTY_COMMON_COMMAND_ID, cmd.pid);
            return p;
        }

        // Liste des opcodes vendor annoncés par le driver (requête WPD, rien n'est envoyé de dangereux)
        public List<uint> GetSupportedVendorOpcodes()
        {
            IPortableDeviceValues p = BuildCommand(Wpd.CMD_GET_SUPPORTED_VENDOR_OPCODES);
            IPortableDeviceValues r = Send(p);
            List<uint> result = new List<uint>();
            IPortableDevicePropVariantCollection coll;
            r.GetIPortableDevicePropVariantCollectionValue(ref Wpd.PROP_VENDOR_OPERATION_CODES, out coll);
            uint n = 0;
            coll.GetCount(ref n);
            for (uint i = 0; i < n; i++)
            {
                PropVariant pv = new PropVariant();
                coll.GetAt(i, ref pv);
                result.Add(pv.AsUInt32());
                PropVariantClear(ref pv);
            }
            return result;
        }

        // Exécute une opération PTP en lecture (phase de données device -> hôte).
        public byte[] ExecuteRead(ushort opcode, uint[] opParams, out ushort responseCode, out uint[] responseParams)
        {
            MtpReadOnlyGuard.Check(opcode); // GARDE-FOU — ne pas retirer

            IPortableDeviceValues p = BuildCommand(Wpd.CMD_EXECUTE_WITH_DATA_TO_READ);
            p.SetUnsignedIntegerValue(ref Wpd.PROP_OPERATION_CODE, opcode);
            IPortableDevicePropVariantCollection coll = Wpd.CreatePropVariantCollection();
            if (opParams != null)
            {
                for (int i = 0; i < opParams.Length; i++)
                {
                    PropVariant pv = PropVariant.FromUInt32(opParams[i]);
                    coll.Add(ref pv);
                }
            }
            p.SetIPortableDevicePropVariantCollectionValue(ref Wpd.PROP_OPERATION_PARAMS, coll);

            IPortableDeviceValues r = Send(p);

            string context;
            r.GetStringValue(ref Wpd.PROP_TRANSFER_CONTEXT, out context);
            ulong total = 0;
            try { r.GetUnsignedLargeIntegerValue(ref Wpd.PROP_TRANSFER_TOTAL_DATA_SIZE, out total); }
            catch (Exception) { total = 0; }
            uint optimal = 0x40000;
            try { r.GetUnsignedIntegerValue(ref Wpd.PROP_OPTIMAL_TRANSFER_BUFFER_SIZE, out optimal); }
            catch (Exception) { optimal = 0x40000; }
            if (optimal == 0) optimal = 0x40000;

            bool unknownSize = (total == 0xFFFFFFFFUL);
            MemoryStream data = new MemoryStream();
            try
            {
                ulong received = 0;
                while (unknownSize || received < total)
                {
                    uint want = optimal;
                    if (!unknownSize)
                    {
                        ulong remaining = total - received;
                        if (remaining < want) want = (uint)remaining;
                    }
                    if (want == 0) break;

                    IPortableDeviceValues rp = BuildCommand(Wpd.CMD_READ_DATA);
                    rp.SetStringValue(ref Wpd.PROP_TRANSFER_CONTEXT, context);
                    rp.SetUnsignedIntegerValue(ref Wpd.PROP_TRANSFER_NUM_BYTES_TO_READ, want);
                    rp.SetBufferValue(ref Wpd.PROP_TRANSFER_DATA, new byte[want], want);
                    IPortableDeviceValues rr = Send(rp);

                    uint got = 0;
                    rr.GetUnsignedIntegerValue(ref Wpd.PROP_TRANSFER_NUM_BYTES_READ, out got);
                    if (got > 0)
                    {
                        // Lecture memory-safe via PROPVARIANT (VT_VECTOR|VT_UI1) :
                        // la liberation passe par PropVariantClear, jamais par un
                        // CoTaskMemFree manuel (source du crash v0.1.0).
                        byte[] chunk = GetTransferData(rr, got);
                        if (chunk != null) data.Write(chunk, 0, chunk.Length);
                    }
                    received += got;
                    if (got == 0) break;              // sécurité anti-boucle infinie
                    if (unknownSize && got < want) break;
                }
            }
            finally
            {
                // Toujours clore le transfert pour récupérer le code réponse PTP
                IPortableDeviceValues ep = BuildCommand(Wpd.CMD_END_DATA_TRANSFER);
                ep.SetStringValue(ref Wpd.PROP_TRANSFER_CONTEXT, context);
                IPortableDeviceValues er = Send(ep);
                uint code = 0;
                er.GetUnsignedIntegerValue(ref Wpd.PROP_RESPONSE_CODE, out code);
                responseCode = (ushort)(code & 0xFFFF);
                responseParams = ReadResponseParams(er);
            }
            return data.ToArray();
        }

        // Extrait le buffer WPD_PROPERTY_MTP_EXT_TRANSFER_DATA d'une collection de
        // resultats, via GetValue -> PROPVARIANT (VT_VECTOR|VT_UI1) + PropVariantClear.
        byte[] GetTransferData(IPortableDeviceValues values, uint expected)
        {
            PropVariant pv;
            values.GetValue(ref Wpd.PROP_TRANSFER_DATA, out pv);
            try
            {
                if (pv.vt != 0x1011) return null; // VT_VECTOR | VT_UI1
                uint cElems = unchecked((uint)(pv.p.ToInt64() & 0xFFFFFFFFL));
                IntPtr pElems = pv.p2;
                uint n = Math.Min(cElems, expected);
                if (pElems == IntPtr.Zero || n == 0) return null;
                byte[] buf = new byte[n];
                Marshal.Copy(pElems, buf, 0, (int)n);
                return buf;
            }
            finally { PropVariantClear(ref pv); }
        }

        uint[] ReadResponseParams(IPortableDeviceValues r)
        {
            List<uint> res = new List<uint>();
            try
            {
                IPortableDevicePropVariantCollection coll;
                r.GetIPortableDevicePropVariantCollectionValue(ref Wpd.PROP_RESPONSE_PARAMS, out coll);
                uint n = 0;
                coll.GetCount(ref n);
                for (uint i = 0; i < n; i++)
                {
                    PropVariant pv = new PropVariant();
                    coll.GetAt(i, ref pv);
                    res.Add(pv.AsUInt32());
                    PropVariantClear(ref pv);
                }
            }
            catch (Exception) { }
            return res.ToArray();
        }

        public void Dispose()
        {
            if (_device != null)
            {
                try { _device.Close(); } catch (Exception) { }
                Marshal.ReleaseComObject(_device);
                _device = null;
            }
        }
    }

    // ------------------------------------------------------------------
    // Analyse des datasets PTP (PIMA 15740)
    // ------------------------------------------------------------------

    public class PtpReader
    {
        byte[] _b;
        int _pos;
        public PtpReader(byte[] b) { _b = b; _pos = 0; }
        public int Remaining { get { return _b.Length - _pos; } }
        public byte U8() { return _b[_pos++]; }
        public ushort U16() { ushort v = (ushort)(_b[_pos] | (_b[_pos + 1] << 8)); _pos += 2; return v; }
        public uint U32() { uint v = (uint)(_b[_pos] | (_b[_pos + 1] << 8) | (_b[_pos + 2] << 16) | (_b[_pos + 3] << 24)); _pos += 4; return v; }
        public ulong U64() { ulong lo = U32(); ulong hi = U32(); return lo | (hi << 32); }

        public string PtpString()
        {
            if (Remaining < 1) return "";
            byte n = U8();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                if (Remaining < 2) break;
                char c = (char)U16();
                if (c != '\0') sb.Append(c);
            }
            return sb.ToString();
        }

        public ushort[] U16Array()
        {
            uint n = U32();
            if (n > 4096) n = 4096; // garde-fou parsing
            ushort[] arr = new ushort[n];
            for (uint i = 0; i < n; i++) arr[i] = U16();
            return arr;
        }

        // Lit une valeur selon le datatype PTP ; retourne sa représentation décimale
        // (long signé quand c'est un type signé) ou une chaîne.
        public object Value(ushort datatype)
        {
            switch (datatype)
            {
                case 0x0001: return (long)(sbyte)U8();
                case 0x0002: return (long)U8();
                case 0x0003: return (long)(short)U16();
                case 0x0004: return (long)U16();
                case 0x0005: return (long)(int)U32();
                case 0x0006: return (long)U32();
                case 0x0007: return (long)U64();
                case 0x0008: return U64();
                case 0xFFFF: return PtpString();
                default:
                    if ((datatype & 0x4000) != 0)
                    {
                        // tableau : u32 count + éléments
                        ushort elemType = (ushort)(datatype & 0xBFFF);
                        uint n = U32();
                        if (n > 4096) n = 4096;
                        List<object> list = new List<object>();
                        for (uint i = 0; i < n; i++) list.Add(Value(elemType));
                        return list;
                    }
                    return null; // INT128/UINT128 ou inconnu : non parsé
            }
        }

        public static string DatatypeName(ushort dt)
        {
            switch (dt)
            {
                case 0x0001: return "INT8";
                case 0x0002: return "UINT8";
                case 0x0003: return "INT16";
                case 0x0004: return "UINT16";
                case 0x0005: return "INT32";
                case 0x0006: return "UINT32";
                case 0x0007: return "INT64";
                case 0x0008: return "UINT64";
                case 0x0009: return "INT128";
                case 0x000A: return "UINT128";
                case 0x4001: return "AINT8";
                case 0x4002: return "AUINT8";
                case 0x4003: return "AINT16";
                case 0x4004: return "AUINT16";
                case 0x4005: return "AINT32";
                case 0x4006: return "AUINT32";
                case 0xFFFF: return "STRING";
                default: return string.Format("0x{0:X4}", dt);
            }
        }

        public static string ResponseName(ushort code)
        {
            switch (code)
            {
                case 0x2001: return "OK";
                case 0x2002: return "GeneralError";
                case 0x2003: return "SessionNotOpen";
                case 0x2004: return "InvalidTransactionID";
                case 0x2005: return "OperationNotSupported";
                case 0x2006: return "ParameterNotSupported";
                case 0x2007: return "IncompleteTransfer";
                case 0x2009: return "InvalidObjectHandle";
                case 0x200A: return "DevicePropNotSupported";
                case 0x200F: return "AccessDenied";
                case 0x2013: return "StoreNotAvailable";
                case 0x2019: return "DeviceBusy";
                case 0x201B: return "InvalidDevicePropFormat";
                case 0x201C: return "InvalidDevicePropValue";
                case 0x201D: return "InvalidParameter";
                case 0x201E: return "SessionAlreadyOpen";
                default: return string.Format("0x{0:X4}", code);
            }
        }
    }

    public class DeviceInfo
    {
        public ushort StandardVersion;
        public uint VendorExtensionID;
        public ushort VendorExtensionVersion;
        public string VendorExtensionDesc = "";
        public ushort FunctionalMode;
        public ushort[] Operations = new ushort[0];
        public ushort[] Events = new ushort[0];
        public ushort[] DeviceProperties = new ushort[0];
        public ushort[] CaptureFormats = new ushort[0];
        public ushort[] PlaybackFormats = new ushort[0];
        public string Manufacturer = "";
        public string Model = "";
        public string DeviceVersion = "";
        public string SerialNumber = "";

        public static DeviceInfo Parse(byte[] data)
        {
            PtpReader r = new PtpReader(data);
            DeviceInfo d = new DeviceInfo();
            d.StandardVersion = r.U16();
            d.VendorExtensionID = r.U32();
            d.VendorExtensionVersion = r.U16();
            d.VendorExtensionDesc = r.PtpString();
            d.FunctionalMode = r.U16();
            d.Operations = r.U16Array();
            d.Events = r.U16Array();
            d.DeviceProperties = r.U16Array();
            d.CaptureFormats = r.U16Array();
            d.PlaybackFormats = r.U16Array();
            d.Manufacturer = r.PtpString();
            d.Model = r.PtpString();
            d.DeviceVersion = r.PtpString();
            d.SerialNumber = r.PtpString();
            return d;
        }
    }

    public class PropDesc
    {
        public ushort Code;
        public ushort Datatype;
        public byte GetSet;
        public object FactoryDefault;
        public object CurrentValue;
        public byte FormFlag;
        public object RangeMin, RangeMax, RangeStep;
        public List<object> EnumValues;

        public static PropDesc Parse(byte[] data)
        {
            PtpReader r = new PtpReader(data);
            PropDesc d = new PropDesc();
            d.Code = r.U16();
            d.Datatype = r.U16();
            d.GetSet = r.U8();
            d.FactoryDefault = r.Value(d.Datatype);
            d.CurrentValue = r.Value(d.Datatype);
            if (r.Remaining > 0)
            {
                d.FormFlag = r.U8();
                if (d.FormFlag == 1 && r.Remaining > 0)
                {
                    d.RangeMin = r.Value(d.Datatype);
                    d.RangeMax = r.Value(d.Datatype);
                    d.RangeStep = r.Value(d.Datatype);
                }
                else if (d.FormFlag == 2 && r.Remaining >= 2)
                {
                    ushort n = r.U16();
                    d.EnumValues = new List<object>();
                    for (int i = 0; i < n && r.Remaining > 0; i++)
                        d.EnumValues.Add(r.Value(d.Datatype));
                }
            }
            return d;
        }
    }

    // ------------------------------------------------------------------
    // Table de noms connus (issue de la recherche — voir docs/)
    // ------------------------------------------------------------------

    public static class KnownProps
    {
        public class Entry
        {
            public string Name;
            public string Source;
            public Entry(string n, string s) { Name = n; Source = s; }
        }

        public static Dictionary<ushort, Entry> Table = BuildTable();

        static void A(Dictionary<ushort, Entry> t, int code, string name, string src)
        {
            t[(ushort)code] = new Entry(name, src);
        }

        static Dictionary<ushort, Entry> BuildTable()
        {
            Dictionary<ushort, Entry> t = new Dictionary<ushort, Entry>();
            const string G = "libgphoto2 ptp.h (PTP_DPC_FUJI_*)";
            const string C = "Filmcase + fujifilm-ptp-recipes (bloc recette, mode RAW CONV)";
            const string L = "libfuji/fudge (petabyt)";

            // --- Cluster recettes / custom settings (communautaire) ---
            A(t, 0xD18C, "CustomSlotSelector (C1-C7, 1..7)", C);
            A(t, 0xD18D, "CustomSlotName (PTP string)", C);
            A(t, 0xD18E, "(bloc recette - non mappé)", C);
            A(t, 0xD18F, "(bloc recette - non mappé)", C);
            A(t, 0xD190, "Recipe.DynamicRange", C);
            A(t, 0xD191, "Recipe.DRangePriority", C);
            A(t, 0xD192, "Recipe.FilmSimulation", C);
            A(t, 0xD193, "Recipe.MonochromaticColor WarmCool", C);
            A(t, 0xD194, "Recipe.MonochromaticColor MagentaGreen", C);
            A(t, 0xD195, "Recipe.GrainEffect", C);
            A(t, 0xD196, "Recipe.ColorChromeEffect", C);
            A(t, 0xD197, "Recipe.ColorChromeFXBlue", C);
            A(t, 0xD198, "Recipe.SmoothSkinEffect", "fujifilm-ptp-recipes uniquement");
            A(t, 0xD199, "Recipe.WhiteBalance", C);
            A(t, 0xD19A, "Recipe.WBShiftR (signé, -9..+9)", C);
            A(t, 0xD19B, "Recipe.WBShiftB (signé, -9..+9)", C);
            A(t, 0xD19C, "Recipe.WBColorTemperature (K)", C);
            A(t, 0xD19D, "Recipe.HighlightTone (x10)", C);
            A(t, 0xD19E, "Recipe.ShadowTone (x10)", C);
            A(t, 0xD19F, "Recipe.Color (x10)", C);
            A(t, 0xD1A0, "Recipe.Sharpness (x10)", C);
            A(t, 0xD1A1, "Recipe.HighISONR (table non-lineaire)", C);
            A(t, 0xD1A2, "Recipe.Clarity (x10) [absent sur X-T30?]", C);
            A(t, 0xD1A3, "(bloc recette - non mappé)", C);
            A(t, 0xD1A4, "(bloc recette - non mappé)", C);
            A(t, 0xD1A5, "(bloc recette - non mappé)", C);

            // --- Cluster conversion RAW (X RAW Studio) ---
            A(t, 0xD183, "StartRawConversion", L);
            A(t, 0xD184, "IOPCode (id processeur)", G);
            A(t, 0xD185, "RawConvProfile (profil binaire X RAW Studio)", G);
            A(t, 0xD186, "TetherRawConditionCode", G);
            A(t, 0xD187, "TetherRawCompatibilityCode", G);
            A(t, 0xD21C, "(expérimental, usage inconnu — vu dans libfuji)", L);

            // --- Divers importants ---
            A(t, 0xD153, "FirmwareVersion", G);
            A(t, 0xD154, "ShotCount", G);
            A(t, 0xD155, "ShutterExchangeCount", G);
            A(t, 0xD15D, "SetUSBMode", G);
            A(t, 0xD16E, "USBMode (5=Tether, 6=RawConv, 8=Webcam)", L);
            A(t, 0xD212, "CurrentState / EventsList", G);
            A(t, 0xD242, "BatteryLevel", G);
            A(t, 0xD34C, "CustomSetting (piste banques C1-C7 ?)", G);
            A(t, 0xD36A, "BatteryInfo1", G);
            A(t, 0xD36B, "BatteryInfo2 (batterie, chaîne)", G);

            // --- Cluster image 0xD0xx (libgphoto2) ---
            A(t, 0xD001, "FilmSimulation (mode tether)", G);
            A(t, 0xD002, "FilmSimulationTune", G);
            A(t, 0xD007, "DRangeMode (tether)", G);
            A(t, 0xD008, "ColorMode (tether)", G);
            A(t, 0xD00A, "ColorSpace", G);
            A(t, 0xD00B, "WhitebalanceTune1", G);
            A(t, 0xD00C, "WhitebalanceTune2", G);
            A(t, 0xD017, "ColorTemperature (tether)", G);
            A(t, 0xD018, "Quality", G);
            A(t, 0xD019, "RecMode", G);
            A(t, 0xD01C, "NoiseReduction", G);
            A(t, 0xD022, "RawCompression", G);
            A(t, 0xD023, "GrainEffect (tether) / PROP_PING (Filmcase)", G);
            A(t, 0xD024, "SetEyeAFMode", G);
            A(t, 0xD025, "FocusPoints", G);
            A(t, 0xD029, "Shadowing", G);
            A(t, 0xD02A, "ExposureIndex", G);
            A(t, 0xD02B, "MovieISO", G);
            A(t, 0xD02E, "WideDynamicRange", G);

            // --- Cluster réglages 0xD1xx (libgphoto2, sélection) ---
            A(t, 0xD100, "Comment", G);
            A(t, 0xD101, "SerialMode", G);
            A(t, 0xD102, "ExposureDelay", G);
            A(t, 0xD104, "BlackImageTone", G);
            A(t, 0xD106, "FrameGuideMode", G);
            A(t, 0xD10A, "ShutterPriorityMode1", G);
            A(t, 0xD10B, "ShutterPriorityMode2", G);
            A(t, 0xD112, "AFIlluminator", G);
            A(t, 0xD113, "Beep", G);
            A(t, 0xD114, "AELock", G);
            A(t, 0xD118, "ExposureStep", G);
            A(t, 0xD119, "CompensationStep", G);
            A(t, 0xD12E, "BKT", G);
            A(t, 0xD145, "Password", G);
            A(t, 0xD147, "CommandDialSetting1", G);
            A(t, 0xD14B, "ButtonsAndDials", G);
            A(t, 0xD15A, "Language", G);
            A(t, 0xD15B, "FrameNumberSequence", G);
            A(t, 0xD15C, "VideoMode", G);
            A(t, 0xD161, "CommentWriteSetting", G);
            A(t, 0xD167, "CommentEx", G);
            A(t, 0xD16F, "CropMode", G);
            A(t, 0xD170, "LensZoomPos", G);
            A(t, 0xD171, "FocusPosition", G);
            A(t, 0xD173, "LiveViewImageQuality", G);
            A(t, 0xD174, "LiveViewImageSize", G);
            A(t, 0xD176, "StandbyMode", G);
            A(t, 0xD17C, "FocusMeteringMode", G);
            A(t, 0xD17F, "ResetSetting", G);

            // --- Cluster capture/état 0xD2xx (libgphoto2, sélection) ---
            A(t, 0xD200, "LightTune", G);
            A(t, 0xD201, "ReleaseMode", G);
            A(t, 0xD204, "BKTStep", G);
            A(t, 0xD206, "FocusAreas", G);
            A(t, 0xD207, "PriorityMode", G);
            A(t, 0xD209, "AFStatus", G);
            A(t, 0xD20B, "DeviceName", G);
            A(t, 0xD20C, "MediaRecord", G);
            A(t, 0xD20D, "MediaCapacity", G);
            A(t, 0xD211, "MediaStatus", G);
            A(t, 0xD215, "Copyright", G);
            A(t, 0xD216, "Copyright2", G);
            A(t, 0xD218, "Aperture", G);
            A(t, 0xD219, "ShutterSpeed", G);
            A(t, 0xD21B, "DeviceError", G);
            A(t, 0xD229, "CaptureRemaining", G);
            A(t, 0xD22A, "MovieRemainingTime", G);
            A(t, 0xD240, "ShutterSpeed2", G);
            A(t, 0xD241, "ImageAspectRatio", G);

            // --- Cluster custom/affichage 0xD3xx (libgphoto2, sélection) ---
            A(t, 0xD310, "TotalShotCount", G);
            A(t, 0xD320, "HighLightTone (menu)", G);
            A(t, 0xD321, "ShadowTone (menu)", G);
            A(t, 0xD322, "LongExposureNR", G);
            A(t, 0xD332, "ISODialHn1", G);
            A(t, 0xD347, "FocusPoint", G);
            A(t, 0xD35E, "FocusCheckMode", G);
            A(t, 0xD365, "FileNamePrefix1", G);
            A(t, 0xD366, "FileNamePrefix2", G);
            A(t, 0xD36D, "LensNameAndSerial", G);
            A(t, 0xD36E, "CustomDispInfo", G);
            A(t, 0xD38C, "LensZoomPosCaps", G);
            A(t, 0xD38D, "LensFNumberList", G);
            A(t, 0xD38E, "LensFocalLengthList", G);

            return t;
        }

        public static string NameOf(ushort code)
        {
            Entry e;
            if (Table.TryGetValue(code, out e)) return e.Name;
            return "(inconnu)";
        }

        public static string SourceOf(ushort code)
        {
            Entry e;
            if (Table.TryGetValue(code, out e)) return e.Source;
            return "-";
        }
    }

    // ------------------------------------------------------------------
    // Mini-sérialiseur JSON (aucune dépendance)
    // ------------------------------------------------------------------

    public static class Json
    {
        public static string Serialize(object o)
        {
            StringBuilder sb = new StringBuilder();
            Write(sb, o, 0);
            return sb.ToString();
        }

        static void Indent(StringBuilder sb, int n) { sb.Append('\n'); for (int i = 0; i < n; i++) sb.Append("  "); }

        static void Write(StringBuilder sb, object o, int depth)
        {
            if (o == null) { sb.Append("null"); return; }
            if (o is string) { WriteString(sb, (string)o); return; }
            if (o is bool) { sb.Append(((bool)o) ? "true" : "false"); return; }
            if (o is Dictionary<string, object>)
            {
                Dictionary<string, object> d = (Dictionary<string, object>)o;
                sb.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> kv in d)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Indent(sb, depth + 1);
                    WriteString(sb, kv.Key);
                    sb.Append(": ");
                    Write(sb, kv.Value, depth + 1);
                }
                if (!first) Indent(sb, depth);
                sb.Append('}');
                return;
            }
            System.Collections.IEnumerable en = o as System.Collections.IEnumerable;
            if (en != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (object item in en)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Indent(sb, depth + 1);
                    Write(sb, item, depth + 1);
                }
                if (!first) Indent(sb, depth);
                sb.Append(']');
                return;
            }
            if (o is double || o is float)
            {
                sb.Append(Convert.ToDouble(o).ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            // entiers (int, uint, long, ulong, ushort, byte...)
            sb.Append(Convert.ToString(o, CultureInfo.InvariantCulture));
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append(string.Format("\\u{0:x4}", (int)c));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------- Parseur (relecture des rapports) ----------

        public static object Parse(string s)
        {
            int pos = 0;
            return ParseValue(s, ref pos);
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++;
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                i++;
                Dictionary<string, object> d = new Dictionary<string, object>();
                SkipWs(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs(s, ref i);
                    string k = ParseString(s, ref i);
                    SkipWs(s, ref i);
                    if (s[i] != ':') throw new FormatException("JSON: ':' attendu");
                    i++;
                    d[k] = ParseValue(s, ref i);
                    SkipWs(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("JSON: ',' ou '}' attendu");
                }
            }
            if (c == '[')
            {
                i++;
                List<object> l = new List<object>();
                SkipWs(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(ParseValue(s, ref i));
                    SkipWs(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("JSON: ',' ou ']' attendu");
                }
            }
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') { i += 4; return true; }
            if (c == 'f') { i += 5; return false; }
            if (c == 'n') { i += 4; return null; }
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            string num = s.Substring(start, i - start);
            long lv;
            if (long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out lv)) return lv;
            return double.Parse(num, CultureInfo.InvariantCulture);
        }

        static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("JSON: '\"' attendu");
            i++;
            StringBuilder sb = new StringBuilder();
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16));
                            i += 4;
                            break;
                    }
                    i++;
                }
                else sb.Append(s[i++]);
            }
            i++;
            return sb.ToString();
        }
    }

    // ------------------------------------------------------------------
    // Décodage humain des valeurs du bloc recette (sources : docs/02)
    // ------------------------------------------------------------------

    public static class RecipeDecode
    {
        public static string FilmSim(long v)
        {
            switch ((int)v)
            {
                case 1: return "PROVIA / Standard";
                case 2: return "Velvia / Vivid";
                case 3: return "ASTIA / Soft";
                case 4: return "PRO Neg. Hi";
                case 5: return "PRO Neg. Std";
                case 6: return "Monochrome";
                case 7: return "Monochrome + Ye";
                case 8: return "Monochrome + R";
                case 9: return "Monochrome + G";
                case 10: return "Sepia";
                case 11: return "Classic Chrome";
                case 12: return "ACROS";
                case 13: return "ACROS + Ye";
                case 14: return "ACROS + R";
                case 15: return "ACROS + G";
                case 16: return "ETERNA";
                case 17: return "Classic Negative (indispo X-T30 !)";
                case 18: return "Eterna Bleach Bypass (indispo X-T30 !)";
                case 19: return "Nostalgic Neg. (indispo X-T30 !)";
                case 20: return "REALA ACE (indispo X-T30 !)";
                default: return "code " + v;
            }
        }

        public static string WhiteBalance(long v)
        {
            switch ((int)v)
            {
                case 0x0002: return "Auto";
                case 0x0004: return "Ensoleillé";
                case 0x0006: return "Incandescent";
                case 0x0008: return "Sous-marin";
                case 0x8001: return "Fluo 1";
                case 0x8002: return "Fluo 2";
                case 0x8003: return "Fluo 3";
                case 0x8006: return "Ombre";
                case 0x8007: return "Température (K)";
                case 0x8008: return "Personnalisée 1";
                case 0x8009: return "Personnalisée 2";
                case 0x800A: return "Personnalisée 3";
                case 0x8020: return "Auto priorité blanc";
                case 0x8021: return "Auto ambiance";
                default: return "code 0x" + v.ToString("X4");
            }
        }

        public static string DynamicRange(long v)
        {
            if (v == 0 || v == 65535) return "AUTO";
            return "DR" + v;
        }

        public static string DRangePriority(long v)
        {
            switch ((int)v)
            {
                case 0: return "Off";
                case 1: return "Faible";
                case 2: return "Fort";
                case 32768: return "Auto";
                default: return "code " + v;
            }
        }

        public static string Grain(long v)
        {
            switch ((int)v)
            {
                case 1: return "Off";
                case 6: return "Off";
                case 7: return "Off";
                case 2: return "Faible / Petit";
                case 3: return "Fort / Petit";
                case 4: return "Faible / Grand";
                case 5: return "Fort / Grand";
                default: return "code " + v;
            }
        }

        public static string OffWeakStrong(long v)
        {
            switch ((int)v)
            {
                case 1: return "Off";
                case 2: return "Faible";
                case 3: return "Fort";
                default: return "code " + v;
            }
        }

        public static string X10(long v)
        {
            if (v == -32768) return "(défaut/inconnu)";
            double d = v / 10.0;
            return (d > 0 ? "+" : "") + d.ToString("0.#", CultureInfo.InvariantCulture);
        }

        public static string Direct(long v)
        {
            return (v > 0 ? "+" : "") + v.ToString();
        }

        public static string HighIsoNr(long v)
        {
            switch ((int)v)
            {
                case 20480: return "+4";
                case 24576: return "+3";
                case 0: return "+2";
                case 4096: return "+1";
                case 8192: return "0";
                case 12288: return "-1";
                case 16384: return "-2";
                case 28672: return "-3";
                case 32768: return "-4";
                default: return "code " + v;
            }
        }

        // Décodage selon le code propriété ; v = valeur entière lue
        public static string For(ushort code, long v)
        {
            switch (code)
            {
                case 0xD18C: return "Slot C" + v;
                case 0xD190: return DynamicRange(v);
                case 0xD191: return DRangePriority(v);
                case 0xD192: return FilmSim(v);
                case 0xD193: return X10(v) + " (chaud/froid)";
                case 0xD194: return X10(v) + " (magenta/vert)";
                case 0xD195: return Grain(v);
                case 0xD196: return OffWeakStrong(v);
                case 0xD197: return OffWeakStrong(v);
                case 0xD198: return OffWeakStrong(v);
                case 0xD199: return WhiteBalance(v);
                case 0xD19A: return "R " + Direct(v);
                case 0xD19B: return "B " + Direct(v);
                case 0xD19C: return v + " K";
                case 0xD19D: return X10(v);
                case 0xD19E: return X10(v);
                case 0xD19F: return X10(v);
                case 0xD1A0: return X10(v);
                case 0xD1A1: return HighIsoNr(v);
                case 0xD1A2: return X10(v);
                default: return v.ToString() + " (0x" + v.ToString("X") + ")";
            }
        }
    }

    // ------------------------------------------------------------------
    // Programme principal
    // ------------------------------------------------------------------

    public static class Program
    {
        static StringBuilder TxtReport = new StringBuilder();

        // Branché par l'interface graphique pour recevoir le journal en direct.
        public static Action<string> LogSink;

        // Journal de session ecrit ligne a ligne (AutoFlush) : survit a un crash brutal.
        static StreamWriter SessionLog;

        static void OpenSessionLog(string outDir)
        {
            try
            {
                if (SessionLog != null) { SessionLog.Close(); SessionLog = null; }
                SessionLog = new StreamWriter(Path.Combine(outDir, "probe-session.log"), true, new UTF8Encoding(false));
                SessionLog.AutoFlush = true;
                SessionLog.WriteLine();
                SessionLog.WriteLine("################ SESSION {0} ################",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception) { SessionLog = null; }
        }

        // Trace un crash dans crash.log (a cote de l'exe) + dans le journal de session.
        public static void LogCrash(object ex)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] CRASH\r\n" + ex + "\r\n\r\n");
            }
            catch (Exception) { }
            try { if (SessionLog != null) SessionLog.WriteLine("!!! CRASH : {0}", ex); }
            catch (Exception) { }
        }

        static void Say(string fmt, params object[] args)
        {
            string line = (args.Length > 0) ? string.Format(fmt, args) : fmt;
            Console.WriteLine(line);
            TxtReport.AppendLine(line);
            try { if (SessionLog != null) SessionLog.WriteLine("{0:HH:mm:ss.fff}  {1}", DateTime.Now, line); }
            catch (Exception) { }
            Action<string> sink = LogSink;
            if (sink != null) sink(line);
        }

        // Propriétés candidates sondées explicitement même si absentes du GetDeviceInfo
        static ushort[] ExplicitProbes = new ushort[] {
            0xD153, 0xD15D, 0xD16E,
            0xD183, 0xD184, 0xD185, 0xD186, 0xD187,
            0xD18C, 0xD18D, 0xD18E, 0xD18F,
            0xD190, 0xD191, 0xD192, 0xD193, 0xD194, 0xD195, 0xD196, 0xD197,
            0xD198, 0xD199, 0xD19A, 0xD19B, 0xD19C, 0xD19D, 0xD19E, 0xD19F,
            0xD1A0, 0xD1A1, 0xD1A2, 0xD1A3, 0xD1A4, 0xD1A5,
            0xD21C, 0xD34C
        };

        [MTAThread]
        public static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException +=
                delegate(object s, UnhandledExceptionEventArgs e) { LogCrash(e.ExceptionObject); };

            bool listOnly = false;
            bool sweep = false;
            string outDir = Directory.GetCurrentDirectory();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--list") listOnly = true;
                else if (args[i] == "--sweep") sweep = true;
                else if (args[i] == "--out" && i + 1 < args.Length) { outDir = args[i + 1]; i++; }
                else if (args[i] == "--help" || args[i] == "-h" || args[i] == "/?")
                {
                    Console.WriteLine("xt30-probe v0.1.0 — sonde PTP EN LECTURE SEULE pour Fujifilm");
                    Console.WriteLine("Usage : xt30-probe [--list] [--sweep] [--out <dossier>]");
                    Console.WriteLine("  --list   liste seulement les périphériques WPD détectés");
                    Console.WriteLine("  --sweep  balaye aussi 0xD000..0xD3FF via GetDevicePropDesc (lecture seule)");
                    Console.WriteLine("  --out    dossier de sortie des rapports (défaut : dossier courant)");
                    return 0;
                }
            }

            return Run(listOnly, sweep, outDir);
        }

        // Codes retour : 0 = OK, 1 = erreur WPD, 2 = aucun Fujifilm, 3 = ouverture impossible
        public static int Run(bool listOnly, bool sweep, string outDir)
        {
            TxtReport.Length = 0;
            OpenSessionLog(outDir);

            Say("=====================================================");
            Say("xt30-probe v0.1.0 — MODE LECTURE SEULE");
            Say("Opcodes autorises : 0x1001, 0x1014, 0x1015 (lecture)");
            Say("Aucune ecriture ne sera envoyee a l'appareil.");
            Say("Date : {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Say("=====================================================");
            Say("");

            Dictionary<string, object> report = new Dictionary<string, object>();
            report["tool"] = "xt30-probe";
            report["version"] = "0.1.0";
            report["readOnly"] = true;
            report["generatedAt"] = DateTime.Now.ToString("o");

            // 1. Énumération
            List<string> ids;
            try { ids = MtpDevice.ListDeviceIds(); }
            catch (Exception ex)
            {
                Say("ERREUR : impossible d'enumerer les peripheriques WPD : {0}", ex.Message);
                return 1;
            }

            List<object> deviceList = new List<object>();
            string fujiId = null;
            Say("Peripheriques WPD detectes : {0}", ids.Count);
            foreach (string id in ids)
            {
                string friendly = MtpDevice.GetDeviceString(id, 0);
                string manuf = MtpDevice.GetDeviceString(id, 1);
                string desc = MtpDevice.GetDeviceString(id, 2);
                bool isFuji = id.ToLowerInvariant().Contains("vid_04cb")
                    || manuf.ToUpperInvariant().Contains("FUJI")
                    || friendly.ToUpperInvariant().Contains("FUJI")
                    || desc.ToUpperInvariant().Contains("FUJI");
                Say("  - {0}", id);
                Say("    Nom: '{0}'  Fabricant: '{1}'  Description: '{2}'  Fujifilm: {3}",
                    friendly, manuf, desc, isFuji ? "OUI" : "non");
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["pnpId"] = id;
                d["friendlyName"] = friendly;
                d["manufacturer"] = manuf;
                d["description"] = desc;
                d["isFuji"] = isFuji;
                deviceList.Add(d);
                if (isFuji && fujiId == null) fujiId = id;
            }
            report["windowsDeviceList"] = deviceList;
            Say("");

            if (listOnly)
            {
                WriteReports(report, outDir);
                return 0;
            }

            if (fujiId == null)
            {
                Say("AUCUN APPAREIL FUJIFILM DETECTE.");
                Say("");
                Say("Verifications :");
                Say(" 1. L'appareil est-il allume et branche en USB ?");
                Say(" 2. MENU -> CONNECTION SETTING -> USB MODE :");
                Say("    choisir 'USB RAW CONV./BACKUP RESTORE' (recommande)");
                Say("    (le mode 'USB CARD READER' n'expose PAS le protocole PTP)");
                Say(" 3. L'appareil apparait-il dans l'Explorateur Windows / Gestionnaire de peripheriques ?");
                WriteReports(report, outDir);
                return 2;
            }

            // 2. Ouverture + sondage
            Dictionary<string, object> devReport = new Dictionary<string, object>();
            report["device"] = devReport;
            devReport["pnpId"] = fujiId;

            using (MtpDevice dev = new MtpDevice())
            {
                try
                {
                    dev.Open(fujiId);
                    Say("FUJIFILM CAMERA DETECTED");
                    Say("Connexion WPD/PTP : OK  (session geree par le driver Windows)");
                    Say("");
                }
                catch (Exception ex)
                {
                    Say("ERREUR : ouverture du peripherique impossible : {0}", ex.Message);
                    Say("Fermez toute application utilisant l'appareil (Explorateur, X Acquire, etc.) puis reessayez.");
                    devReport["openError"] = ex.ToString();
                    WriteReports(report, outDir);
                    return 3;
                }

                // Opcodes vendor annoncés par le driver
                try
                {
                    List<uint> vendorOps = dev.GetSupportedVendorOpcodes();
                    List<object> vl = new List<object>();
                    StringBuilder sb = new StringBuilder();
                    foreach (uint op in vendorOps) { vl.Add(string.Format("0x{0:X4}", op)); sb.AppendFormat("0x{0:X4} ", op); }
                    devReport["vendorOpcodesReportedByDriver"] = vl;
                    Say("Opcodes vendor annonces par le driver : {0}", sb.Length > 0 ? sb.ToString() : "(aucun)");
                    Say("");
                }
                catch (Exception ex)
                {
                    devReport["vendorOpcodesError"] = ex.Message;
                    Say("(Requete des opcodes vendor non disponible : {0})", ex.Message);
                    Say("");
                }

                // GetDeviceInfo
                DeviceInfo info = null;
                try
                {
                    ushort rc;
                    uint[] rp;
                    byte[] data = dev.ExecuteRead(0x1001, new uint[0], out rc, out rp);
                    devReport["getDeviceInfoResponse"] = string.Format("0x{0:X4} ({1})", rc, PtpReader.ResponseName(rc));
                    devReport["getDeviceInfoRawHex"] = Hex(data);
                    if (data.Length > 0)
                    {
                        info = DeviceInfo.Parse(data);
                        Say("GetDeviceInfo (0x1001) : reponse 0x{0:X4} ({1}), {2} octets", rc, PtpReader.ResponseName(rc), data.Length);
                        Say("");
                        Say("  Manufacturer     : {0}", info.Manufacturer);
                        Say("  Model            : {0}", info.Model);
                        Say("  DeviceVersion    : {0}", info.DeviceVersion);
                        Say("  SerialNumber     : {0}", info.SerialNumber);
                        Say("  StandardVersion  : {0}", info.StandardVersion);
                        Say("  VendorExtension  : ID=0x{0:X8} v{1} '{2}'", info.VendorExtensionID, info.VendorExtensionVersion, info.VendorExtensionDesc);
                        Say("  Operations       : {0}", HexList(info.Operations));
                        Say("  Events           : {0}", HexList(info.Events));
                        Say("  DeviceProperties : {0}", HexList(info.DeviceProperties));
                        Say("");

                        Dictionary<string, object> di = new Dictionary<string, object>();
                        di["standardVersion"] = info.StandardVersion;
                        di["vendorExtensionId"] = string.Format("0x{0:X8}", info.VendorExtensionID);
                        di["vendorExtensionVersion"] = info.VendorExtensionVersion;
                        di["vendorExtensionDesc"] = info.VendorExtensionDesc;
                        di["functionalMode"] = info.FunctionalMode;
                        di["manufacturer"] = info.Manufacturer;
                        di["model"] = info.Model;
                        di["deviceVersion"] = info.DeviceVersion;
                        di["serialNumber"] = info.SerialNumber;
                        di["operationsSupported"] = HexArray(info.Operations);
                        di["eventsSupported"] = HexArray(info.Events);
                        di["devicePropertiesSupported"] = HexArray(info.DeviceProperties);
                        di["captureFormats"] = HexArray(info.CaptureFormats);
                        di["playbackFormats"] = HexArray(info.PlaybackFormats);
                        devReport["deviceInfo"] = di;
                        WriteReports(report, outDir, false); // sauvegarde immediate du DeviceInfo

                        // Indicateurs clés
                        bool hasSetProp = Contains(info.Operations, 0x1016);
                        bool hasD18C = Contains(info.DeviceProperties, 0xD18C);
                        bool hasD18D = Contains(info.DeviceProperties, 0xD18D);
                        Say("  ANALYSE RAPIDE :");
                        Say("   - SetDevicePropValue (0x1016) annonce : {0}  (jamais utilise par cette sonde)", hasSetProp ? "OUI" : "NON");
                        Say("   - 0xD18C (slot selector) dans DeviceInfo : {0}", hasD18C ? "OUI" : "NON");
                        Say("   - 0xD18D (slot name) dans DeviceInfo     : {0}", hasD18D ? "OUI" : "NON");
                        Say("");
                    }
                }
                catch (Exception ex)
                {
                    devReport["getDeviceInfoError"] = ex.ToString();
                    Say("ERREUR GetDeviceInfo : {0}", ex.Message);
                }

                // 3. Sondage des propriétés
                List<ushort> toProbe = new List<ushort>();
                if (info != null)
                    foreach (ushort p in info.DeviceProperties)
                        if (!toProbe.Contains(p)) toProbe.Add(p);
                foreach (ushort p in ExplicitProbes)
                    if (!toProbe.Contains(p)) toProbe.Add(p);
                toProbe.Sort();

                Say("-----------------------------------------------------");
                Say("SONDAGE DES PROPRIETES ({0} propriétés, lecture seule)", toProbe.Count);
                Say("-----------------------------------------------------");

                List<object> propsReport = new List<object>();
                devReport["properties"] = propsReport;
                int done = 0;
                foreach (ushort code in toProbe)
                {
                    propsReport.Add(ProbeProperty(dev, info, code, true));
                    done++;
                    // sauvegarde incrementale : un crash ne perd plus les donnees
                    if (done % 15 == 0) WriteReports(report, outDir, false);
                    System.Threading.Thread.Sleep(25); // douceur avec l'appareil
                }
                WriteReports(report, outDir, false);

                // 4. Balayage optionnel
                if (sweep)
                {
                    Say("");
                    Say("--- BALAYAGE 0xD000..0xD3FF (GetDevicePropDesc, lecture seule) ---");
                    List<object> sweepReport = new List<object>();
                    for (int c = 0xD000; c <= 0xD3FF; c++)
                    {
                        ushort code = (ushort)c;
                        if (toProbe.Contains(code)) continue;
                        try
                        {
                            ushort rc;
                            uint[] rp;
                            byte[] data = dev.ExecuteRead(0x1014, new uint[] { code }, out rc, out rp);
                            if (rc == 0x2001 && data.Length >= 5)
                            {
                                Say("  0x{0:X4} REPOND ! ({1} octets) -> sonde complete", code, data.Length);
                                sweepReport.Add(ProbeProperty(dev, info, code, false));
                                WriteReports(report, outDir, false);
                            }
                        }
                        catch (Exception) { }
                        System.Threading.Thread.Sleep(10);
                    }
                    devReport["sweepDiscoveries"] = sweepReport;
                }

                // 5. Résumé custom settings
                Say("");
                Say("=====================================================");
                Say("RESUME CUSTOM SETTINGS (C1-C7)");
                Say("=====================================================");
                SummarizeCustom(propsReport);
            }

            WriteReports(report, outDir);
            Say("");
            Say("Termine. Envoyez xt30_report.json et xt30_report.txt pour analyse.");
            return 0;
        }

        static Dictionary<string, object> ProbeProperty(MtpDevice dev, DeviceInfo info, ushort code, bool verbose)
        {
            Dictionary<string, object> pr = new Dictionary<string, object>();
            pr["code"] = string.Format("0x{0:X4}", code);
            pr["name"] = KnownProps.NameOf(code);
            pr["source"] = KnownProps.SourceOf(code);
            bool listed = info != null && Contains(info.DeviceProperties, code);
            pr["listedInDeviceInfo"] = listed;

            if (verbose)
            {
                Say("");
                Say("0x{0:X4}  {1}", code, KnownProps.NameOf(code));
                Say("  Source identification : {0}", KnownProps.SourceOf(code));
                Say("  Annoncee par DeviceInfo : {0}", listed ? "OUI" : "NON (sondee explicitement)");
            }

            // GetDevicePropDesc 0x1014
            try
            {
                ushort rc;
                uint[] rp;
                byte[] data = dev.ExecuteRead(0x1014, new uint[] { code }, out rc, out rp);
                pr["descResponse"] = string.Format("0x{0:X4} ({1})", rc, PtpReader.ResponseName(rc));
                if (rc == 0x2001 && data.Length >= 5)
                {
                    pr["descRawHex"] = Hex(data);
                    PropDesc d = PropDesc.Parse(data);
                    Dictionary<string, object> dd = new Dictionary<string, object>();
                    dd["datatype"] = PtpReader.DatatypeName(d.Datatype);
                    dd["getSet"] = d.GetSet;
                    dd["writableAccordingToDescriptor"] = (d.GetSet == 1);
                    dd["factoryDefault"] = ValueOut(d.FactoryDefault);
                    dd["currentValue"] = ValueOut(d.CurrentValue);
                    dd["formFlag"] = d.FormFlag;
                    if (d.FormFlag == 1)
                    {
                        dd["rangeMin"] = ValueOut(d.RangeMin);
                        dd["rangeMax"] = ValueOut(d.RangeMax);
                        dd["rangeStep"] = ValueOut(d.RangeStep);
                    }
                    else if (d.FormFlag == 2 && d.EnumValues != null)
                    {
                        List<object> ev = new List<object>();
                        foreach (object v in d.EnumValues) ev.Add(ValueOut(v));
                        dd["enumValues"] = ev;
                    }
                    pr["desc"] = dd;
                    if (verbose)
                    {
                        Say("  Supported            : YES");
                        Say("  Datatype             : {0}", PtpReader.DatatypeName(d.Datatype));
                        Say("  Writable (descriptor): {0}  [AUCUNE ECRITURE EFFECTUEE]", d.GetSet == 1 ? "YES" : "NO");
                        Say("  Factory default      : {0}", Display(d.FactoryDefault));
                        Say("  Current value        : {0}", Display(d.CurrentValue));
                        if (d.FormFlag == 1)
                            Say("  Allowed range        : {0} .. {1} (pas {2})", Display(d.RangeMin), Display(d.RangeMax), Display(d.RangeStep));
                        else if (d.FormFlag == 2 && d.EnumValues != null)
                            Say("  Allowed values       : {0}", DisplayList(d.EnumValues));
                    }
                }
                else if (verbose)
                {
                    Say("  Supported            : NO (GetDevicePropDesc -> {0})", PtpReader.ResponseName(rc));
                }
            }
            catch (Exception ex)
            {
                pr["descError"] = ex.Message;
                if (verbose) Say("  GetDevicePropDesc    : ERREUR {0}", ex.Message);
            }

            // GetDevicePropValue 0x1015 (valeur brute)
            try
            {
                ushort rc;
                uint[] rp;
                byte[] data = dev.ExecuteRead(0x1015, new uint[] { code }, out rc, out rp);
                pr["valueResponse"] = string.Format("0x{0:X4} ({1})", rc, PtpReader.ResponseName(rc));
                if (rc == 0x2001)
                {
                    pr["valueRawHex"] = Hex(data);
                    pr["valueRawLength"] = data.Length;
                    if (verbose) Say("  Raw value (0x1015)   : [{0}] {1}", data.Length, Hex(data, 64));
                }
            }
            catch (Exception ex)
            {
                pr["valueError"] = ex.Message;
                if (verbose) Say("  GetDevicePropValue   : ERREUR {0}", ex.Message);
            }

            return pr;
        }

        static void SummarizeCustom(List<object> propsReport)
        {
            ushort[] custom = new ushort[] { 0xD18C, 0xD18D };
            foreach (ushort code in custom)
            {
                string key = string.Format("0x{0:X4}", code);
                Dictionary<string, object> found = null;
                foreach (object o in propsReport)
                {
                    Dictionary<string, object> d = (Dictionary<string, object>)o;
                    if ((string)d["code"] == key) { found = d; break; }
                }
                Say("");
                Say("{0}  ({1})", key, KnownProps.NameOf(code));
                if (found == null) { Say("  Non sondee."); continue; }
                if (found.ContainsKey("desc"))
                {
                    Dictionary<string, object> dd = (Dictionary<string, object>)found["desc"];
                    Say("  Supported : YES");
                    Say("  Writable according to descriptor : {0}", ((bool)dd["writableAccordingToDescriptor"]) ? "YES" : "NO");
                    Say("  Datatype  : {0}", dd["datatype"]);
                    Say("  Current   : {0}", dd["currentValue"]);
                }
                else
                {
                    Say("  Supported : NO / pas de descripteur ({0})",
                        found.ContainsKey("descResponse") ? found["descResponse"] : "erreur");
                }
            }
            Say("");
            Say("RAPPEL : meme si une propriete est 'writable', cette sonde n'ecrit JAMAIS.");
        }

        // ---------- utilitaires ----------

        static bool Contains(ushort[] arr, ushort v)
        {
            if (arr == null) return false;
            for (int i = 0; i < arr.Length; i++) if (arr[i] == v) return true;
            return false;
        }

        static string Hex(byte[] data) { return Hex(data, int.MaxValue); }
        static string Hex(byte[] data, int max)
        {
            StringBuilder sb = new StringBuilder();
            int n = Math.Min(data.Length, max);
            for (int i = 0; i < n; i++) sb.AppendFormat("{0:x2}", data[i]);
            if (n < data.Length) sb.Append("...");
            return sb.ToString();
        }

        static string HexList(ushort[] arr)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < arr.Length; i++) sb.AppendFormat("0x{0:X4} ", arr[i]);
            return sb.ToString();
        }

        static List<object> HexArray(ushort[] arr)
        {
            List<object> l = new List<object>();
            for (int i = 0; i < arr.Length; i++) l.Add(string.Format("0x{0:X4}", arr[i]));
            return l;
        }

        static object ValueOut(object v)
        {
            if (v == null) return null;
            if (v is List<object>)
            {
                List<object> l = new List<object>();
                foreach (object x in (List<object>)v) l.Add(ValueOut(x));
                return l;
            }
            return v;
        }

        static string Display(object v)
        {
            if (v == null) return "(non parse)";
            if (v is string) return "'" + (string)v + "'";
            if (v is List<object>) return DisplayList((List<object>)v);
            if (v is long)
            {
                long x = (long)v;
                if (x >= 0) return string.Format("{0} (0x{0:X})", x);
                return string.Format("{0}", x);
            }
            return v.ToString();
        }

        static string DisplayList(List<object> l)
        {
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < l.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                if (i >= 32) { sb.Append("..."); break; }
                sb.Append(Display(l[i]));
            }
            sb.Append(']');
            return sb.ToString();
        }

        static void WriteReports(Dictionary<string, object> report, string outDir)
        {
            WriteReports(report, outDir, true);
        }

        static void WriteReports(Dictionary<string, object> report, string outDir, bool announce)
        {
            try
            {
                string jsonPath = Path.Combine(outDir, "xt30_report.json");
                string txtPath = Path.Combine(outDir, "xt30_report.txt");
                File.WriteAllText(jsonPath, Json.Serialize(report), new UTF8Encoding(false));
                File.WriteAllText(txtPath, TxtReport.ToString(), new UTF8Encoding(false));
                if (announce)
                {
                    Say("");
                    Say("Rapports ecrits :");
                    Say("  {0}", jsonPath);
                    Say("  {0}", txtPath);
                    // Archive horodatee : chaque scan est conserve dans rapports\
                    try
                    {
                        string archDir = Path.Combine(outDir, "rapports");
                        Directory.CreateDirectory(archDir);
                        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                        File.Copy(jsonPath, Path.Combine(archDir, "xt30_report_" + stamp + ".json"), true);
                        File.Copy(txtPath, Path.Combine(archDir, "xt30_report_" + stamp + ".txt"), true);
                        Say("Archive : rapports\\xt30_report_{0}.json / .txt", stamp);
                    }
                    catch (Exception ex2)
                    {
                        Say("(archivage impossible : {0})", ex2.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                if (announce) Say("ERREUR ecriture rapports : {0}", ex.Message);
            }
        }
    }
}
