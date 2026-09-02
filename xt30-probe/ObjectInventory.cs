// Phase 2: inventaire PTP/MTP strictement en lecture seule.
//
// Ce programme est compile dans un executable separe. Il n'appelle que :
//   0x1004 GetStorageIDs
//   0x1005 GetStorageInfo
//   0x1007 GetObjectHandles
//   0x1008 GetObjectInfo
//
// Il ne telecharge aucun objet. En particulier, GetObject (0x1009) est absent.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Xt30Probe
{
    public sealed class InventoryPtpReader
    {
        readonly byte[] _data;
        int _position;

        public InventoryPtpReader(byte[] data)
        {
            _data = data ?? new byte[0];
            _position = 0;
        }

        public int Position { get { return _position; } }
        public int Remaining { get { return _data.Length - _position; } }

        void Require(int count)
        {
            if (count < 0 || Remaining < count)
                throw new InvalidDataException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Dataset PTP tronque a l'offset {0}: {1} octets requis, {2} disponibles.",
                    _position, count, Remaining));
        }

        public ushort U16()
        {
            Require(2);
            ushort value = (ushort)(_data[_position] | (_data[_position + 1] << 8));
            _position += 2;
            return value;
        }

        public uint U32()
        {
            Require(4);
            uint value = (uint)(_data[_position]
                | (_data[_position + 1] << 8)
                | (_data[_position + 2] << 16)
                | (_data[_position + 3] << 24));
            _position += 4;
            return value;
        }

        public ulong U64()
        {
            ulong low = U32();
            ulong high = U32();
            return low | (high << 32);
        }

        public string PtpString()
        {
            Require(1);
            int charCount = _data[_position++];
            if (charCount == 0) return "";
            Require(charCount * 2);
            StringBuilder value = new StringBuilder();
            for (int i = 0; i < charCount; i++)
            {
                char c = (char)U16();
                if (c != '\0') value.Append(c);
            }
            return value.ToString();
        }
    }

    public static class ObjectInventoryProgram
    {
        static readonly ushort[] InventoryAllowed = new ushort[] {
            0x1004, // GetStorageIDs
            0x1005, // GetStorageInfo
            0x1007, // GetObjectHandles
            0x1008  // GetObjectInfo
        };

        static StreamWriter _log;

        static bool IsAllowed(ushort opcode)
        {
            for (int i = 0; i < InventoryAllowed.Length; i++)
                if (InventoryAllowed[i] == opcode) return true;
            return false;
        }

        static byte[] Read(MtpDevice device, ushort opcode, uint[] parameters,
            out ushort responseCode, out uint[] responseParameters)
        {
            if (!IsAllowed(opcode))
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "GARDE INVENTAIRE: opcode 0x{0:X4} refuse.", opcode));
            return device.ExecuteRead(opcode, parameters, out responseCode, out responseParameters);
        }

        static string Response(ushort code)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "0x{0:X4} ({1})", code, PtpReader.ResponseName(code));
        }

        static string Hex32(uint value)
        {
            return string.Format(CultureInfo.InvariantCulture, "0x{0:X8}", value);
        }

        static string Hex16(ushort value)
        {
            return string.Format(CultureInfo.InvariantCulture, "0x{0:X4}", value);
        }

        static string Hex(byte[] data)
        {
            if (data == null || data.Length == 0) return "";
            StringBuilder result = new StringBuilder(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
                result.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
            return result.ToString();
        }

        static List<object> HexParams(uint[] values)
        {
            List<object> result = new List<object>();
            if (values != null)
                for (int i = 0; i < values.Length; i++) result.Add(Hex32(values[i]));
            return result;
        }

        static Dictionary<string, object> OperationResult(
            ushort opcode, uint[] parameters, ushort responseCode,
            uint[] responseParameters, byte[] data)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["opcode"] = Hex16(opcode);
            result["direction"] = "device-to-host";
            result["parameters"] = HexParams(parameters);
            result["responseCode"] = Response(responseCode);
            result["responseParameters"] = HexParams(responseParameters);
            result["dataLength"] = data == null ? 0 : data.Length;
            return result;
        }

        static List<uint> ParseU32Array(byte[] data)
        {
            InventoryPtpReader reader = new InventoryPtpReader(data);
            uint count = reader.U32();
            if (count > 1000000)
                throw new InvalidDataException("Nombre d'elements PTP non plausible: " + count);
            if ((ulong)count * 4UL > (ulong)reader.Remaining)
                throw new InvalidDataException("Tableau UINT32 PTP tronque.");
            List<uint> result = new List<uint>();
            for (uint i = 0; i < count; i++) result.Add(reader.U32());
            return result;
        }

        static Dictionary<string, object> ParseStorageInfo(byte[] data)
        {
            InventoryPtpReader reader = new InventoryPtpReader(data);
            Dictionary<string, object> info = new Dictionary<string, object>();
            ushort storageType = reader.U16();
            ushort fileSystemType = reader.U16();
            ushort accessCapability = reader.U16();
            info["storageType"] = Hex16(storageType);
            info["fileSystemType"] = Hex16(fileSystemType);
            info["accessCapability"] = Hex16(accessCapability);
            info["maxCapacityBytes"] = reader.U64();
            info["freeSpaceBytes"] = reader.U64();
            info["freeSpaceImages"] = reader.U32();
            info["storageDescription"] = reader.PtpString();
            info["volumeLabel"] = reader.PtpString();
            info["trailingBytes"] = reader.Remaining;
            return info;
        }

        static Dictionary<string, object> ParseObjectInfo(uint requestedHandle, byte[] data)
        {
            InventoryPtpReader reader = new InventoryPtpReader(data);
            Dictionary<string, object> info = new Dictionary<string, object>();
            info["handle"] = Hex32(requestedHandle);
            info["storageID"] = Hex32(reader.U32());
            info["objectFormat"] = Hex16(reader.U16());
            info["protectionStatus"] = reader.U16();
            info["objectCompressedSize"] = reader.U32();
            info["thumbFormat"] = Hex16(reader.U16());
            info["thumbCompressedSize"] = reader.U32();
            info["thumbPixWidth"] = reader.U32();
            info["thumbPixHeight"] = reader.U32();
            info["imagePixWidth"] = reader.U32();
            info["imagePixHeight"] = reader.U32();
            info["imageBitDepth"] = reader.U32();
            info["parentObject"] = Hex32(reader.U32());
            info["associationType"] = reader.U16();
            info["associationDesc"] = reader.U32();
            info["sequenceNumber"] = reader.U32();
            info["filename"] = reader.PtpString();
            info["captureDate"] = reader.PtpString();
            info["modificationDate"] = reader.PtpString();
            info["keywords"] = reader.PtpString();
            info["trailingBytes"] = reader.Remaining;
            info["rawObjectInfoHex"] = Hex(data);
            return info;
        }

        static void WritePtpString(BinaryWriter writer, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                writer.Write((byte)0);
                return;
            }
            writer.Write((byte)(value.Length + 1));
            for (int i = 0; i < value.Length; i++) writer.Write((ushort)value[i]);
            writer.Write((ushort)0);
        }

        static void AssertTest(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("SELF-TEST: " + message);
        }

        static int RunSelfTests()
        {
            // Vérifie les deux barrières, y compris tous les opcodes interdits
            // explicitement cités dans le cahier des charges.
            ushort[] allowed = new ushort[] { 0x1004, 0x1005, 0x1007, 0x1008 };
            AssertTest(InventoryAllowed.Length == allowed.Length,
                "la whitelist inventaire contient un opcode supplémentaire");
            for (int i = 0; i < allowed.Length; i++)
            {
                AssertTest(InventoryAllowed[i] == allowed[i],
                    "ordre ou contenu inattendu de la whitelist inventaire");
                AssertTest(IsAllowed(allowed[i]), "opcode inventaire autorisé refusé");
                MtpReadOnlyGuard.Check(allowed[i]);
            }
            System.Reflection.FieldInfo globalField = typeof(MtpReadOnlyGuard).GetField(
                "Allowed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            AssertTest(globalField != null, "whitelist globale introuvable");
            ushort[] globalAllowed = (ushort[])globalField.GetValue(null);
            ushort[] expectedGlobal = new ushort[] {
                0x1001, 0x1004, 0x1005, 0x1007, 0x1008, 0x1014, 0x1015
            };
            AssertTest(globalAllowed.Length == expectedGlobal.Length,
                "la whitelist globale contient un opcode supplémentaire");
            for (int i = 0; i < expectedGlobal.Length; i++)
                AssertTest(globalAllowed[i] == expectedGlobal[i],
                    "ordre ou contenu inattendu de la whitelist globale");
            ushort[] forbidden = new ushort[] {
                0x1009, 0x100B, 0x100C, 0x100D, 0x1016,
                0x900C, 0x900D, 0x901D
            };
            for (int i = 0; i < forbidden.Length; i++)
            {
                AssertTest(!IsAllowed(forbidden[i]), "opcode interdit dans la liste inventaire");
                bool rejected = false;
                try { MtpReadOnlyGuard.Check(forbidden[i]); }
                catch (InvalidOperationException) { rejected = true; }
                AssertTest(rejected, string.Format(CultureInfo.InvariantCulture,
                    "opcode interdit 0x{0:X4} accepté par MtpReadOnlyGuard", forbidden[i]));
            }

            // GetStorageIDs: count=2, puis deux UINT32 little-endian.
            MemoryStream idsStream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(idsStream, Encoding.UTF8, true))
            {
                writer.Write((uint)2);
                writer.Write((uint)0x00010001);
                writer.Write((uint)0x00020001);
            }
            List<uint> ids = ParseU32Array(idsStream.ToArray());
            AssertTest(ids.Count == 2 && ids[0] == 0x00010001 && ids[1] == 0x00020001,
                "parsing GetStorageIDs incorrect");

            // Dataset StorageInfo complet, avec capacités UINT64 et deux chaînes PTP.
            MemoryStream storageStream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(storageStream, Encoding.UTF8, true))
            {
                writer.Write((ushort)0x0003);
                writer.Write((ushort)0x0002);
                writer.Write((ushort)0x0000);
                writer.Write((ulong)64000000000UL);
                writer.Write((ulong)32000000000UL);
                writer.Write((uint)1234);
                WritePtpString(writer, "SDCARD");
                WritePtpString(writer, "FUJI_SD");
            }
            Dictionary<string, object> storage = ParseStorageInfo(storageStream.ToArray());
            AssertTest((string)storage["storageType"] == "0x0003", "StorageType incorrect");
            AssertTest((ulong)storage["maxCapacityBytes"] == 64000000000UL, "MaxCapacity incorrect");
            AssertTest((string)storage["storageDescription"] == "SDCARD", "StorageDescription incorrect");
            AssertTest((string)storage["volumeLabel"] == "FUJI_SD", "VolumeLabel incorrect");
            AssertTest((int)storage["trailingBytes"] == 0, "octets StorageInfo non consommés");

            // Dataset ObjectInfo avec chaque champ standard renseigné.
            MemoryStream objectStream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(objectStream, Encoding.UTF8, true))
            {
                writer.Write((uint)0x00010001); // StorageID
                writer.Write((ushort)0x3801);  // ObjectFormat JPEG
                writer.Write((ushort)1);       // ProtectionStatus
                writer.Write((uint)1234567);   // ObjectCompressedSize
                writer.Write((ushort)0x3801);  // ThumbFormat
                writer.Write((uint)4096);
                writer.Write((uint)160);
                writer.Write((uint)120);
                writer.Write((uint)6240);
                writer.Write((uint)4160);
                writer.Write((uint)24);
                writer.Write((uint)0x00000042); // ParentObject
                writer.Write((ushort)1);        // AssociationType
                writer.Write((uint)7);          // AssociationDesc
                writer.Write((uint)99);         // SequenceNumber
                WritePtpString(writer, "DSCF0001.JPG");
                WritePtpString(writer, "20260831T230000");
                WritePtpString(writer, "20260831T230100");
                WritePtpString(writer, "TEST");
            }
            Dictionary<string, object> oi = ParseObjectInfo(0x12345678, objectStream.ToArray());
            AssertTest((string)oi["handle"] == "0x12345678", "handle ObjectInfo incorrect");
            AssertTest((string)oi["storageID"] == "0x00010001", "StorageID ObjectInfo incorrect");
            AssertTest((string)oi["objectFormat"] == "0x3801", "ObjectFormat incorrect");
            AssertTest((uint)oi["objectCompressedSize"] == 1234567, "taille objet incorrecte");
            AssertTest((string)oi["parentObject"] == "0x00000042", "ParentObject incorrect");
            AssertTest((ushort)oi["associationType"] == 1, "AssociationType incorrect");
            AssertTest((string)oi["filename"] == "DSCF0001.JPG", "Filename incorrect");
            AssertTest((string)oi["captureDate"] == "20260831T230000", "CaptureDate incorrecte");
            AssertTest((int)oi["trailingBytes"] == 0, "octets ObjectInfo non consommés");

            bool truncatedRejected = false;
            try { ParseObjectInfo(1, new byte[12]); }
            catch (InvalidDataException) { truncatedRejected = true; }
            AssertTest(truncatedRejected, "dataset ObjectInfo tronqué accepté");

            Dictionary<string, object> jsonProbe = new Dictionary<string, object>();
            jsonProbe["readOnly"] = true;
            jsonProbe["object"] = oi;
            object parsed = Json.Parse(Json.Serialize(jsonProbe));
            AssertTest(parsed is Dictionary<string, object>, "rapport JSON non relisible");

            Console.WriteLine("SELF-TEST OK: garde-fous, StorageIDs, StorageInfo, ObjectInfo, troncature et JSON.");
            return 0;
        }

        static void Log(string format, params object[] args)
        {
            string line = args.Length == 0 ? format : string.Format(CultureInfo.InvariantCulture, format, args);
            Console.WriteLine(line);
            if (_log != null) { _log.WriteLine(line); _log.Flush(); }
        }

        static Dictionary<string, object> ReadObjectInfo(MtpDevice device, uint handle, bool special)
        {
            ushort responseCode;
            uint[] responseParameters;
            byte[] data = Read(device, 0x1008, new uint[] { handle }, out responseCode, out responseParameters);
            Dictionary<string, object> result = OperationResult(
                0x1008, new uint[] { handle }, responseCode, responseParameters, data);
            result["handle"] = Hex32(handle);
            result["specialHandleProbe"] = special;
            if (responseCode == 0x2001 && data.Length > 0)
            {
                try { result["objectInfo"] = ParseObjectInfo(handle, data); }
                catch (Exception ex)
                {
                    result["parseError"] = ex.Message;
                    result["rawDataHex"] = Hex(data);
                }
            }
            Log("GetObjectInfo({0}) -> {1}, {2} octets", Hex32(handle), Response(responseCode), data.Length);
            return result;
        }

        static string FindFujiDevice(List<object> listed, List<string> ids)
        {
            string selected = null;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                string friendly = MtpDevice.GetDeviceString(id, 0);
                string manufacturer = MtpDevice.GetDeviceString(id, 1);
                string description = MtpDevice.GetDeviceString(id, 2);
                bool isFuji = id.ToLowerInvariant().Contains("vid_04cb")
                    || friendly.ToUpperInvariant().Contains("FUJI")
                    || manufacturer.ToUpperInvariant().Contains("FUJI")
                    || description.ToUpperInvariant().Contains("FUJI");
                Dictionary<string, object> entry = new Dictionary<string, object>();
                entry["pnpId"] = id;
                entry["friendlyName"] = friendly;
                entry["manufacturer"] = manufacturer;
                entry["description"] = description;
                entry["isFujifilm"] = isFuji;
                listed.Add(entry);
                if (selected == null && isFuji) selected = id;
            }
            return selected;
        }

        [MTAThread]
        public static int Main(string[] args)
        {
            string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "phase2-inventory");
            bool selfTest = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--out" && i + 1 < args.Length) { outDir = args[++i]; }
                else if (args[i] == "--self-test") selfTest = true;
                else if (args[i] == "--help" || args[i] == "-h" || args[i] == "/?")
                {
                    Console.WriteLine("Usage: xt30-object-inventory.exe [--out <dossier>]");
                    Console.WriteLine("Lecture seule: 0x1004, 0x1005, 0x1007, 0x1008 uniquement.");
                    Console.WriteLine("  --self-test  valide hors ligne parseurs et garde-fous");
                    return 0;
                }
                else
                {
                    Console.Error.WriteLine("Argument inconnu: " + args[i]);
                    return 64;
                }
            }

            if (selfTest) return RunSelfTests();

            Directory.CreateDirectory(outDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string jsonPath = Path.Combine(outDir, "ptp-object-inventory-" + stamp + ".json");
            string logPath = Path.Combine(outDir, "ptp-object-inventory-" + stamp + ".log");
            _log = new StreamWriter(logPath, false, new UTF8Encoding(false));

            Dictionary<string, object> report = new Dictionary<string, object>();
            report["tool"] = "xt30-object-inventory";
            report["generatedAt"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            report["readOnly"] = true;
            report["transport"] = "Windows WPD MTP extension passthrough; data phase device-to-host";
            report["allowedOpcodes"] = new object[] { "0x1004", "0x1005", "0x1007", "0x1008" };
            report["explicitlyAbsentOpcodes"] = new object[] {
                "0x1009 GetObject", "0x100B DeleteObject", "0x100C SendObjectInfo",
                "0x100D SendObject", "0x1016 SetDevicePropValue",
                "0x900C Fuji SendObjectInfo", "0x900D Fuji SendObject2", "0x901D Fuji SendObject"
            };
            report["specialHandleZeroEvidence"] =
                "grawji camera_backup.py read_backup(): standard GetObjectInfo(0), then GetObject(0); this run performs metadata-only GetObjectInfo(0).";

            int exitCode = 0;
            try
            {
                Log("XT30 PHASE 2 - PTP OBJECT INVENTORY - STRICT READ ONLY");
                Log("Opcodes: 0x1004, 0x1005, 0x1007, 0x1008 uniquement");
                Log("Aucun objet n'est telecharge; aucun opcode vendor n'est execute.");

                List<string> ids = MtpDevice.ListDeviceIds();
                List<object> deviceList = new List<object>();
                string fujiId = FindFujiDevice(deviceList, ids);
                report["windowsDevices"] = deviceList;
                if (fujiId == null)
                {
                    report["result"] = "NO_FUJIFILM_DEVICE";
                    Log("Aucun appareil Fujifilm WPD detecte.");
                    exitCode = 2;
                }
                else
                {
                    using (MtpDevice device = new MtpDevice())
                    {
                        device.Open(fujiId);
                        report["result"] = "OPENED";
                        Log("Appareil Fujifilm ouvert via WPD.");

                        ushort storageResponse;
                        uint[] storageResponseParameters;
                        byte[] storageData = Read(device, 0x1004, new uint[0],
                            out storageResponse, out storageResponseParameters);
                        Dictionary<string, object> storageIdsOperation = OperationResult(
                            0x1004, new uint[0], storageResponse, storageResponseParameters, storageData);
                        report["getStorageIDs"] = storageIdsOperation;
                        List<uint> storageIds = new List<uint>();
                        if (storageResponse == 0x2001)
                        {
                            storageIds = ParseU32Array(storageData);
                            List<object> encoded = new List<object>();
                            for (int i = 0; i < storageIds.Count; i++) encoded.Add(Hex32(storageIds[i]));
                            storageIdsOperation["storageIDs"] = encoded;
                        }
                        Log("GetStorageIDs -> {0}, {1} storage(s)", Response(storageResponse), storageIds.Count);

                        List<object> storages = new List<object>();
                        List<object> objects = new List<object>();
                        HashSet<uint> seenHandles = new HashSet<uint>();
                        Dictionary<string, object> handleZeroFromEnumeration = null;
                        for (int s = 0; s < storageIds.Count; s++)
                        {
                            uint storageId = storageIds[s];
                            Dictionary<string, object> storage = new Dictionary<string, object>();
                            storage["storageID"] = Hex32(storageId);
                            storages.Add(storage);

                            ushort infoResponse;
                            uint[] infoResponseParameters;
                            byte[] infoData = Read(device, 0x1005, new uint[] { storageId },
                                out infoResponse, out infoResponseParameters);
                            Dictionary<string, object> infoOperation = OperationResult(
                                0x1005, new uint[] { storageId }, infoResponse, infoResponseParameters, infoData);
                            storage["getStorageInfo"] = infoOperation;
                            if (infoResponse == 0x2001 && infoData.Length > 0)
                            {
                                try { infoOperation["storageInfo"] = ParseStorageInfo(infoData); }
                                catch (Exception ex)
                                {
                                    infoOperation["parseError"] = ex.Message;
                                    infoOperation["rawDataHex"] = Hex(infoData);
                                }
                            }
                            Log("GetStorageInfo({0}) -> {1}", Hex32(storageId), Response(infoResponse));

                            ushort handlesResponse;
                            uint[] handlesResponseParameters;
                            uint[] handlesParameters = new uint[] { storageId, 0, 0 };
                            byte[] handlesData = Read(device, 0x1007, handlesParameters,
                                out handlesResponse, out handlesResponseParameters);
                            Dictionary<string, object> handlesOperation = OperationResult(
                                0x1007, handlesParameters, handlesResponse, handlesResponseParameters, handlesData);
                            storage["getObjectHandles"] = handlesOperation;
                            List<uint> handles = new List<uint>();
                            if (handlesResponse == 0x2001)
                            {
                                handles = ParseU32Array(handlesData);
                                List<object> encodedHandles = new List<object>();
                                for (int h = 0; h < handles.Count; h++) encodedHandles.Add(Hex32(handles[h]));
                                handlesOperation["handles"] = encodedHandles;
                            }
                            Log("GetObjectHandles({0}, 0, 0) -> {1}, {2} handle(s)",
                                Hex32(storageId), Response(handlesResponse), handles.Count);

                            for (int h = 0; h < handles.Count; h++)
                            {
                                uint handle = handles[h];
                                if (seenHandles.Add(handle))
                                {
                                    Dictionary<string, object> objectRecord = ReadObjectInfo(device, handle, false);
                                    objects.Add(objectRecord);
                                    if (handle == 0)
                                    {
                                        objectRecord["specialHandleProbe"] = true;
                                        objectRecord["discoveredByGetObjectHandles"] = true;
                                        handleZeroFromEnumeration = objectRecord;
                                    }
                                }
                            }
                        }
                        report["storages"] = storages;
                        report["ordinaryHandleCount"] = seenHandles.Count;
                        report["objects"] = objects;

                        // Handle 0 is not guessed: grawji documents and uses it as the
                        // Fuji settings-backup object. Only ObjectInfo metadata is read.
                        report["specialHandleZero"] = handleZeroFromEnumeration != null
                            ? handleZeroFromEnumeration
                            : ReadObjectInfo(device, 0, true);
                        report["result"] = "INVENTORY_COMPLETE";
                    }
                }
            }
            catch (Exception ex)
            {
                report["result"] = "ERROR";
                report["error"] = ex.ToString();
                Log("ERREUR: {0}", ex);
                exitCode = 1;
            }
            finally
            {
                File.WriteAllText(jsonPath, Json.Serialize(report), new UTF8Encoding(false));
                Log("Rapport JSON: {0}", jsonPath);
                if (_log != null) { _log.Dispose(); _log = null; }
            }
            return exitCode;
        }
    }
}
