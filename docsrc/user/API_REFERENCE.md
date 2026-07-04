# TOYOPUC Computerlink .NET API Reference

This page is generated from the `PlcComm.Toyopuc` assembly public API and XML documentation comments.

Run `python scripts/generate_api_reference.py --help` from the repository root to regenerate it.

## PlcComm.Toyopuc

### ClockData

```csharp
public sealed class ClockData
```

#### Members

##### ClockData

```csharp
public ClockData(int Second, int Minute, int Hour, int Day, int Month, int Year2Digit, int Weekday)
```

##### AsDateTime

```csharp
public DateTime AsDateTime(int yearBase = 2000)
```

##### Second

```csharp
public int Second { get; set; }
```

##### Minute

```csharp
public int Minute { get; set; }
```

##### Hour

```csharp
public int Hour { get; set; }
```

##### Day

```csharp
public int Day { get; set; }
```

##### Month

```csharp
public int Month { get; set; }
```

##### Year2Digit

```csharp
public int Year2Digit { get; set; }
```

##### Weekday

```csharp
public int Weekday { get; set; }
```

### CpuStatusData

```csharp
public sealed class CpuStatusData
```

#### Members

##### CpuStatusData

```csharp
public CpuStatusData(byte data1, byte data2, byte data3, byte data4, byte data5, byte data6, byte data7, byte data8)
```

##### RawHex

```csharp
public string RawHex()
```

##### Data1

```csharp
public byte Data1 { get; }
```

##### Data2

```csharp
public byte Data2 { get; }
```

##### Data3

```csharp
public byte Data3 { get; }
```

##### Data4

```csharp
public byte Data4 { get; }
```

##### Data5

```csharp
public byte Data5 { get; }
```

##### Data6

```csharp
public byte Data6 { get; }
```

##### Data7

```csharp
public byte Data7 { get; }
```

##### Data8

```csharp
public byte Data8 { get; }
```

##### RawBytes

```csharp
public byte[] RawBytes { get; }
```

##### RawBytesHex

```csharp
public string RawBytesHex { get; }
```

##### Run

```csharp
public bool Run { get; }
```

##### UnderStop

```csharp
public bool UnderStop { get; }
```

##### UnderStopRequestContinuity

```csharp
public bool UnderStopRequestContinuity { get; }
```

##### UnderPseudoStop

```csharp
public bool UnderPseudoStop { get; }
```

##### DebugMode

```csharp
public bool DebugMode { get; }
```

##### IoMonitorUserMode

```csharp
public bool IoMonitorUserMode { get; }
```

##### Pc3Mode

```csharp
public bool Pc3Mode { get; }
```

##### Pc10Mode

```csharp
public bool Pc10Mode { get; }
```

##### FatalFailure

```csharp
public bool FatalFailure { get; }
```

##### FaintFailure

```csharp
public bool FaintFailure { get; }
```

##### Alarm

```csharp
public bool Alarm { get; }
```

##### IoAllocationParameterAltered

```csharp
public bool IoAllocationParameterAltered { get; }
```

##### WithMemoryCard

```csharp
public bool WithMemoryCard { get; }
```

##### MemoryCardOperation

```csharp
public bool MemoryCardOperation { get; }
```

##### WriteProtectedProgramInfo

```csharp
public bool WriteProtectedProgramInfo { get; }
```

##### ReadProtectedSystemMemory

```csharp
public bool ReadProtectedSystemMemory { get; }
```

##### WriteProtectedSystemMemory

```csharp
public bool WriteProtectedSystemMemory { get; }
```

##### ReadProtectedSystemIo

```csharp
public bool ReadProtectedSystemIo { get; }
```

##### WriteProtectedSystemIo

```csharp
public bool WriteProtectedSystemIo { get; }
```

##### Trace

```csharp
public bool Trace { get; }
```

##### ScanSamplingTrace

```csharp
public bool ScanSamplingTrace { get; }
```

##### PeriodicSamplingTrace

```csharp
public bool PeriodicSamplingTrace { get; }
```

##### EnableDetected

```csharp
public bool EnableDetected { get; }
```

##### TriggerDetected

```csharp
public bool TriggerDetected { get; }
```

##### OneScanStep

```csharp
public bool OneScanStep { get; }
```

##### OneBlockStep

```csharp
public bool OneBlockStep { get; }
```

##### OneInstructionStep

```csharp
public bool OneInstructionStep { get; }
```

##### IoOffline

```csharp
public bool IoOffline { get; }
```

##### RemoteRunSetting

```csharp
public bool RemoteRunSetting { get; }
```

##### StatusLatchSetting

```csharp
public bool StatusLatchSetting { get; }
```

##### WritePriorityLimitedProgramInfo

```csharp
public bool WritePriorityLimitedProgramInfo { get; }
```

##### AbnormalWriteFlashRegister

```csharp
public bool AbnormalWriteFlashRegister { get; }
```

##### UnderWritingFlashRegister

```csharp
public bool UnderWritingFlashRegister { get; }
```

##### AbnormalWriteEquipmentInfo

```csharp
public bool AbnormalWriteEquipmentInfo { get; }
```

##### AbnormalWritingEquipmentInfo

```csharp
public bool AbnormalWritingEquipmentInfo { get; }
```

##### AbnormalWriteDuringRun

```csharp
public bool AbnormalWriteDuringRun { get; }
```

##### UnderWritingDuringRun

```csharp
public bool UnderWritingDuringRun { get; }
```

##### Program3Running

```csharp
public bool Program3Running { get; }
```

##### Program2Running

```csharp
public bool Program2Running { get; }
```

##### Program1Running

```csharp
public bool Program1Running { get; }
```

### ExNoAddress32

```csharp
public sealed class ExNoAddress32
```

#### Members

##### ExNoAddress32

```csharp
public ExNoAddress32(int ExNo, int Address, string Unit)
```

##### ExNo

```csharp
public int ExNo { get; set; }
```

##### Address

```csharp
public int Address { get; set; }
```

##### Unit

```csharp
public string Unit { get; set; }
```

### ExtNoAddress

```csharp
public sealed class ExtNoAddress
```

#### Members

##### ExtNoAddress

```csharp
public ExtNoAddress(int No, int Address, string Unit)
```

##### No

```csharp
public int No { get; set; }
```

##### Address

```csharp
public int Address { get; set; }
```

##### Unit

```csharp
public string Unit { get; set; }
```

### ParsedAddress

```csharp
public sealed class ParsedAddress
```

#### Members

##### ParsedAddress

```csharp
public ParsedAddress(string Area, int Index, string Unit, bool High = false, bool Packed = false, int? DigitCount = null)
```

##### Area

```csharp
public string Area { get; set; }
```

##### Index

```csharp
public int Index { get; set; }
```

##### Unit

```csharp
public string Unit { get; set; }
```

##### High

```csharp
public bool High { get; set; }
```

##### Packed

```csharp
public bool Packed { get; set; }
```

##### DigitCount

```csharp
public int? DigitCount { get; set; }
```

### QueuedToyopucDeviceClient

```csharp
public sealed class QueuedToyopucDeviceClient
```

A wrapper for `ToyopucDeviceClient` that serializes compound async operations.

Remarks: Use this wrapper when one TOYOPUC transport session must serve multiple high-level reads, writes, poll cycles, or relay operations without overlapping command lifetimes.

#### Members

##### QueuedToyopucDeviceClient

```csharp
public QueuedToyopucDeviceClient(ToyopucDeviceClient client, object relayHops = null)
```

Initializes a new instance of the `QueuedToyopucDeviceClient` class.

Parameters:
- `client`: The underlying TOYOPUC client.
- `relayHops`: Optional relay hop configuration.

##### OpenAsync

```csharp
public Task OpenAsync(CancellationToken cancellationToken = default)
```

Opens the connection asynchronously with exclusive access.

Remarks: Relay and direct sessions both flow through this gate-protected open path.

##### ExecuteAsync

```csharp
public Task<T> ExecuteAsync<T>(Func<ToyopucDeviceClient, Task<T>> operation, CancellationToken cancellationToken = default)
```

Executes an async operation with exclusive access to the wrapped client.

Returns: The value returned by `operation`.

Parameters:
- `operation`: Delegate that receives the wrapped `ToyopucDeviceClient`.
- `cancellationToken`: Cancellation token used while waiting for exclusive access.

##### ExecuteAsync

```csharp
public Task ExecuteAsync(Func<ToyopucDeviceClient, Task> operation, CancellationToken cancellationToken = default)
```

Executes an async operation with exclusive access to the wrapped client.

Parameters:
- `operation`: Delegate that receives the wrapped `ToyopucDeviceClient`.
- `cancellationToken`: Cancellation token used while waiting for exclusive access.

##### Dispose

```csharp
public void Dispose()
```

Disposes the wrapper and the underlying client.

##### DisposeAsync

```csharp
public ValueTask DisposeAsync()
```

Disposes the wrapper and the underlying client asynchronously.

##### InnerClient

```csharp
public ToyopucDeviceClient InnerClient { get; }
```

Gets the wrapped low-level client.

Remarks: Use `CancellationToken)` when you need direct client access while preserving serialized execution.

##### RelayHops

```csharp
public IReadOnlyList<ValueTuple<int, int>> RelayHops { get; }
```

Gets the configured relay hops, if any.

Remarks: The returned sequence is already normalized to link/station tuples.

##### UsesRelay

```csharp
public bool UsesRelay { get; }
```

Gets a value indicating whether relay mode is enabled.

##### Host

```csharp
public string Host { get; }
```

Gets the PLC host.

##### Port

```csharp
public int Port { get; }
```

Gets the PLC port.

##### Transport

```csharp
public ToyopucTransportMode Transport { get; }
```

Gets the selected transport protocol.

##### Timeout

```csharp
public TimeSpan Timeout { get; set; }
```

Gets or sets the operation timeout.

##### PlcProfile

```csharp
public string PlcProfile { get; }
```

Gets the normalized PLC profile name.

##### AddressingOptions

```csharp
public ToyopucAddressingOptions AddressingOptions { get; }
```

Gets the addressing options used by the wrapped client.

##### CaptureTraceFrames

```csharp
public bool CaptureTraceFrames { get; set; }
```

Gets or sets a value indicating whether transport trace frames are captured.

##### TraceHook

```csharp
public Action<ToyopucTraceFrame> TraceHook { get; set; }
```

Gets or sets the raw trace callback.

##### IsOpen

```csharp
public bool IsOpen { get; }
```

Gets a value indicating whether the underlying transport is open.

### RelayLayer

```csharp
public sealed class RelayLayer
```

#### Members

##### RelayLayer

```csharp
public RelayLayer(int LinkNo, int StationNo, int Ack, byte[] InnerRaw, byte[] Padding = null)
```

##### LinkNo

```csharp
public int LinkNo { get; set; }
```

##### StationNo

```csharp
public int StationNo { get; set; }
```

##### Ack

```csharp
public int Ack { get; set; }
```

##### InnerRaw

```csharp
public byte[] InnerRaw { get; set; }
```

##### Padding

```csharp
public byte[] Padding { get; set; }
```

### ResolvedDevice

```csharp
public sealed class ResolvedDevice
```

#### Members

##### ResolvedDevice

```csharp
public ResolvedDevice(string Text, string Scheme, string Unit, string Area, int Index, string Prefix = null, bool High = false, bool Packed = false, int? BasicAddress = null, int? No = null, int? Address = null, int? BitNo = null, int? Address32 = null)
```

##### Text

```csharp
public string Text { get; set; }
```

##### Scheme

```csharp
public string Scheme { get; set; }
```

##### Unit

```csharp
public string Unit { get; set; }
```

##### Area

```csharp
public string Area { get; set; }
```

##### Index

```csharp
public int Index { get; set; }
```

##### Prefix

```csharp
public string Prefix { get; set; }
```

##### High

```csharp
public bool High { get; set; }
```

##### Packed

```csharp
public bool Packed { get; set; }
```

##### BasicAddress

```csharp
public int? BasicAddress { get; set; }
```

##### No

```csharp
public int? No { get; set; }
```

##### Address

```csharp
public int? Address { get; set; }
```

##### BitNo

```csharp
public int? BitNo { get; set; }
```

##### Address32

```csharp
public int? Address32 { get; set; }
```

### ResponseFrame

```csharp
public sealed class ResponseFrame
```

#### Members

##### ResponseFrame

```csharp
public ResponseFrame(byte Ft, byte Rc, byte Cmd, byte[] Data)
```

##### Ft

```csharp
public byte Ft { get; set; }
```

##### Rc

```csharp
public byte Rc { get; set; }
```

##### Cmd

```csharp
public byte Cmd { get; set; }
```

##### Data

```csharp
public byte[] Data { get; set; }
```

### ToyopucAddress

```csharp
public static class ToyopucAddress
```

Public helpers for TOYOPUC address parsing, formatting, normalization, and low-level encoding.

Remarks: This type serves two audiences: Applications that need canonical address text for generated documentation or UI. Low-level tooling that needs to encode resolved addresses into transport-specific numeric forms. The higher-level parse, format, and normalize methods are the recommended public entry points for most callers.

#### Members

##### Parse

```csharp
public static ResolvedDevice Parse(string text, ToyopucAddressingOptions options = null, string profile = null)
```

Parses a canonical device string into a resolved device shape.

Returns: The resolved device shape.

Parameters:
- `text`: Canonical or profile-aware device text such as `D0000`, `P1-D0000`, or `M0000`.
- `options`: Optional explicit addressing options.
- `profile`: Optional PLC profile name used to resolve profile-specific address rules.

##### TryParse

```csharp
public static bool TryParse(string text, out ResolvedDevice address)
```

Attempts to parse a canonical device string into a resolved device shape.

Returns: `true` when parsing succeeds; otherwise `false`.

Parameters:
- `text`: Device text to parse.
- `address`: When this method returns `true`, receives the resolved device.

##### TryParse

```csharp
public static bool TryParse(string text, ToyopucAddressingOptions options, string profile, out ResolvedDevice address)
```

Attempts to parse a canonical device string into a resolved device shape.

Returns: `true` when parsing succeeds; otherwise `false`.

Parameters:
- `text`: Device text to parse.
- `address`: When this method returns `true`, receives the resolved device.

##### Format

```csharp
public static string Format(ResolvedDevice address)
```

Formats a resolved device back to canonical text.

Returns: Canonical uppercase device text.

Parameters:
- `address`: Resolved device to format.

##### Format

```csharp
public static string Format(ResolvedDevice address, string profile)
```

Formats a resolved device back to canonical text using an explicit PLC profile.

Returns: Canonical uppercase device text.

Parameters:
- `address`: Resolved device to format.
- `profile`: Canonical PLC profile name.

##### Format

```csharp
public static string Format(ResolvedDevice address, int index)
```

Formats a resolved device using an explicit index override.

Returns: Canonical uppercase device text for the supplied index.

Parameters:
- `address`: Resolved device metadata to reuse.
- `index`: Explicit logical index to format.

##### Format

```csharp
public static string Format(ResolvedDevice address, int index, string profile)
```

Formats a resolved device using an explicit index override and PLC profile.

Returns: Canonical uppercase device text for the supplied index.

Parameters:
- `address`: Resolved device metadata to reuse.
- `index`: Explicit logical index to format.
- `profile`: Canonical PLC profile name.

##### Normalize

```csharp
public static string Normalize(string text, ToyopucAddressingOptions options = null, string profile = null)
```

Normalizes a device string to canonical casing and width.

Returns: The canonical representation returned by `ResolvedDevice)`.

Parameters:
- `text`: Input device text in any supported spelling.
- `options`: Optional explicit addressing options.
- `profile`: Optional profile name used by the resolver.

##### ParseAddress

```csharp
public static ParsedAddress ParseAddress(string text, string unit, int radix = 16)
```

##### ParsePrefixedAddress

```csharp
public static ValueTuple<int, ParsedAddress> ParsePrefixedAddress(string text, string unit, int radix = 16)
```

##### EncodeWordAddress

```csharp
public static int EncodeWordAddress(ParsedAddress address)
```

##### EncodeByteAddress

```csharp
public static int EncodeByteAddress(ParsedAddress address)
```

##### EncodeBitAddress

```csharp
public static int EncodeBitAddress(ParsedAddress address)
```

##### EncodePc10BitAddress

```csharp
public static int EncodePc10BitAddress(ParsedAddress address)
```

##### EncodeProgramWordAddress

```csharp
public static int EncodeProgramWordAddress(ParsedAddress address)
```

##### EncodeProgramByteAddress

```csharp
public static int EncodeProgramByteAddress(ParsedAddress address)
```

##### EncodeProgramBitAddress

```csharp
public static ValueTuple<int, int> EncodeProgramBitAddress(ParsedAddress address)
```

##### EncodeExNoBitU32

```csharp
public static int EncodeExNoBitU32(int exNo, int bitAddress)
```

##### EncodeExNoByteU32

```csharp
public static int EncodeExNoByteU32(int exNo, int byteAddress)
```

##### SplitU32Words

```csharp
public static ValueTuple<int, int> SplitU32Words(int value)
```

##### FrBlockExNo

```csharp
public static int FrBlockExNo(int index)
```

##### EncodeFrWordAddr32

```csharp
public static int EncodeFrWordAddr32(int index)
```

##### EncodeExtNoAddress

```csharp
public static ExtNoAddress EncodeExtNoAddress(string area, int index, string unit)
```

### ToyopucAddressingOptions

```csharp
public sealed class ToyopucAddressingOptions
```

#### Members

##### ToyopucAddressingOptions

```csharp
public ToyopucAddressingOptions(bool UseUpperUPc10 = true, bool UseEbPc10 = true, bool UseFrPc10 = true, bool UseUpperBitPc10 = true, bool UseUpperMBitPc10 = true)
```

##### FromProfile

```csharp
public static ToyopucAddressingOptions FromProfile(string profile)
```

##### UseUpperUPc10

```csharp
public bool UseUpperUPc10 { get; set; }
```

##### UseEbPc10

```csharp
public bool UseEbPc10 { get; set; }
```

##### UseFrPc10

```csharp
public bool UseFrPc10 { get; set; }
```

##### UseUpperBitPc10

```csharp
public bool UseUpperBitPc10 { get; set; }
```

##### UseUpperMBitPc10

```csharp
public bool UseUpperMBitPc10 { get; set; }
```

##### Default

```csharp
public static ToyopucAddressingOptions Default { get; }
```

##### Generic

```csharp
public static ToyopucAddressingOptions Generic { get; }
```

##### ToyopucPlusStandard

```csharp
public static ToyopucAddressingOptions ToyopucPlusStandard { get; }
```

##### ToyopucPlusExtended

```csharp
public static ToyopucAddressingOptions ToyopucPlusExtended { get; }
```

##### Nano10GxMode

```csharp
public static ToyopucAddressingOptions Nano10GxMode { get; }
```

##### Nano10GxCompatible

```csharp
public static ToyopucAddressingOptions Nano10GxCompatible { get; }
```

##### Pc10GStandardPc3Jg

```csharp
public static ToyopucAddressingOptions Pc10GStandardPc3Jg { get; }
```

##### Pc10GMode

```csharp
public static ToyopucAddressingOptions Pc10GMode { get; }
```

##### Pc3JxPc3Separate

```csharp
public static ToyopucAddressingOptions Pc3JxPc3Separate { get; }
```

##### Pc3JxPlusExpansion

```csharp
public static ToyopucAddressingOptions Pc3JxPlusExpansion { get; }
```

##### Pc3JgMode

```csharp
public static ToyopucAddressingOptions Pc3JgMode { get; }
```

##### Pc3JgPc3Separate

```csharp
public static ToyopucAddressingOptions Pc3JgPc3Separate { get; }
```

### ToyopucAddressRange

```csharp
public sealed class ToyopucAddressRange
```

#### Members

##### ToyopucAddressRange

```csharp
public ToyopucAddressRange(int Start, int End)
```

##### Contains

```csharp
public bool Contains(int index)
```

##### Start

```csharp
public int Start { get; set; }
```

##### End

```csharp
public int End { get; set; }
```

### ToyopucAreaDescriptor

```csharp
public sealed class ToyopucAreaDescriptor
```

#### Members

##### ToyopucAreaDescriptor

```csharp
public ToyopucAreaDescriptor(string Area, IReadOnlyList<ToyopucAddressRange> DirectRanges, IReadOnlyList<ToyopucAddressRange> PrefixedRanges, bool SupportsPackedWord, int AddressWidth, int SuggestedStartStep, IReadOnlyList<ToyopucAddressRange> PackedDirectRangesOverride = null, IReadOnlyList<ToyopucAddressRange> PackedPrefixedRangesOverride = null)
```

##### UsesDerivedAccess

```csharp
public bool UsesDerivedAccess(string unit, bool packed = false)
```

##### GetAddressWidth

```csharp
public int GetAddressWidth(string unit, bool packed = false)
```

##### GetRanges

```csharp
public IReadOnlyList<ToyopucAddressRange> GetRanges(bool prefixed, bool packed)
```

##### GetRanges

```csharp
public IReadOnlyList<ToyopucAddressRange> GetRanges(bool prefixed, string unit, bool packed = false)
```

##### Area

```csharp
public string Area { get; set; }
```

##### DirectRanges

```csharp
public IReadOnlyList<ToyopucAddressRange> DirectRanges { get; set; }
```

##### PrefixedRanges

```csharp
public IReadOnlyList<ToyopucAddressRange> PrefixedRanges { get; set; }
```

##### SupportsPackedWord

```csharp
public bool SupportsPackedWord { get; set; }
```

##### AddressWidth

```csharp
public int AddressWidth { get; set; }
```

##### SuggestedStartStep

```csharp
public int SuggestedStartStep { get; set; }
```

##### PackedDirectRangesOverride

```csharp
public IReadOnlyList<ToyopucAddressRange> PackedDirectRangesOverride { get; set; }
```

##### PackedPrefixedRangesOverride

```csharp
public IReadOnlyList<ToyopucAddressRange> PackedPrefixedRangesOverride { get; set; }
```

##### DirectRange

```csharp
public ToyopucAddressRange DirectRange { get; }
```

##### PrefixedRange

```csharp
public ToyopucAddressRange PrefixedRange { get; }
```

##### PackedAddressWidth

```csharp
public int PackedAddressWidth { get; }
```

##### DerivedAddressWidth

```csharp
public int DerivedAddressWidth { get; }
```

##### PackedDirectRange

```csharp
public ToyopucAddressRange PackedDirectRange { get; }
```

##### PackedPrefixedRange

```csharp
public ToyopucAddressRange PackedPrefixedRange { get; }
```

##### SupportsDirect

```csharp
public bool SupportsDirect { get; }
```

##### SupportsPrefixed

```csharp
public bool SupportsPrefixed { get; }
```

### ToyopucClient

```csharp
public class ToyopucClient
```

#### Members

##### UdpReceiveBufferSize

```csharp
public const int UdpReceiveBufferSize
```

##### ReadDWord

```csharp
public uint ReadDWord(int address)
```

##### WriteDWord

```csharp
public void WriteDWord(int address, uint value)
```

##### ReadDWords

```csharp
public uint[] ReadDWords(int address, int count)
```

##### WriteDWords

```csharp
public void WriteDWords(int address, IEnumerable<uint> values)
```

##### ReadFloat32

```csharp
public float ReadFloat32(int address)
```

##### WriteFloat32

```csharp
public void WriteFloat32(int address, float value)
```

##### ReadFloat32s

```csharp
public float[] ReadFloat32s(int address, int count)
```

##### WriteFloat32s

```csharp
public void WriteFloat32s(int address, IEnumerable<float> values)
```

##### OpenAsync

```csharp
public virtual Task OpenAsync(CancellationToken cancellationToken = default)
```

##### CloseAsync

```csharp
public virtual Task CloseAsync(CancellationToken cancellationToken = default)
```

##### DisposeAsync

```csharp
public virtual ValueTask DisposeAsync()
```

##### ClearTraceFramesAsync

```csharp
public Task ClearTraceFramesAsync(CancellationToken cancellationToken = default)
```

##### SendRawAsync

```csharp
public Task<ResponseFrame> SendRawAsync(int cmd, byte[] data = null, CancellationToken cancellationToken = default)
```

##### SendPayloadAsync

```csharp
public Task<ResponseFrame> SendPayloadAsync(byte[] payload, CancellationToken cancellationToken = default)
```

##### ReadWordsAsync

```csharp
public Task<int[]> ReadWordsAsync(int address, int count, CancellationToken cancellationToken = default)
```

##### WriteWordsAsync

```csharp
public Task WriteWordsAsync(int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
```

##### ReadBytesAsync

```csharp
public Task<byte[]> ReadBytesAsync(int address, int count, CancellationToken cancellationToken = default)
```

##### WriteBytesAsync

```csharp
public Task WriteBytesAsync(int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
```

##### ReadBitAsync

```csharp
public Task<bool> ReadBitAsync(int address, CancellationToken cancellationToken = default)
```

##### WriteBitAsync

```csharp
public Task WriteBitAsync(int address, bool value, CancellationToken cancellationToken = default)
```

##### ReadDWordAsync

```csharp
public Task<uint> ReadDWordAsync(int address, CancellationToken cancellationToken = default)
```

##### WriteDWordAsync

```csharp
public Task WriteDWordAsync(int address, uint value, CancellationToken cancellationToken = default)
```

##### ReadDWordsAsync

```csharp
public Task<uint[]> ReadDWordsAsync(int address, int count, CancellationToken cancellationToken = default)
```

##### WriteDWordsAsync

```csharp
public Task WriteDWordsAsync(int address, IEnumerable<uint> values, CancellationToken cancellationToken = default)
```

##### ReadFloat32Async

```csharp
public Task<float> ReadFloat32Async(int address, CancellationToken cancellationToken = default)
```

##### WriteFloat32Async

```csharp
public Task WriteFloat32Async(int address, float value, CancellationToken cancellationToken = default)
```

##### ReadFloat32sAsync

```csharp
public Task<float[]> ReadFloat32sAsync(int address, int count, CancellationToken cancellationToken = default)
```

##### WriteFloat32sAsync

```csharp
public Task WriteFloat32sAsync(int address, IEnumerable<float> values, CancellationToken cancellationToken = default)
```

##### ReadWordsMultiAsync

```csharp
public Task<int[]> ReadWordsMultiAsync(IEnumerable<int> addresses, CancellationToken cancellationToken = default)
```

##### WriteWordsMultiAsync

```csharp
public Task WriteWordsMultiAsync(IEnumerable<ValueTuple<int, int>> pairs, CancellationToken cancellationToken = default)
```

##### ReadBytesMultiAsync

```csharp
public Task<byte[]> ReadBytesMultiAsync(IEnumerable<int> addresses, CancellationToken cancellationToken = default)
```

##### WriteBytesMultiAsync

```csharp
public Task WriteBytesMultiAsync(IEnumerable<ValueTuple<int, int>> pairs, CancellationToken cancellationToken = default)
```

##### ReadExtWordsAsync

```csharp
public Task<int[]> ReadExtWordsAsync(int number, int address, int count, CancellationToken cancellationToken = default)
```

##### WriteExtWordsAsync

```csharp
public Task WriteExtWordsAsync(int number, int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
```

##### ReadExtBytesAsync

```csharp
public Task<byte[]> ReadExtBytesAsync(int number, int address, int count, CancellationToken cancellationToken = default)
```

##### WriteExtBytesAsync

```csharp
public Task WriteExtBytesAsync(int number, int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
```

##### ReadExtMultiAsync

```csharp
public Task<byte[]> ReadExtMultiAsync(IEnumerable<ValueTuple<int, int, int>> bitPoints, IEnumerable<ValueTuple<int, int>> bytePoints, IEnumerable<ValueTuple<int, int>> wordPoints, CancellationToken cancellationToken = default)
```

##### WriteExtMultiAsync

```csharp
public Task WriteExtMultiAsync(IEnumerable<ValueTuple<int, int, int, int>> bitPoints, IEnumerable<ValueTuple<int, int, int>> bytePoints, IEnumerable<ValueTuple<int, int, int>> wordPoints, CancellationToken cancellationToken = default)
```

##### Pc10BlockReadAsync

```csharp
public Task<byte[]> Pc10BlockReadAsync(int address32, int count, CancellationToken cancellationToken = default)
```

##### Pc10BlockWriteAsync

```csharp
public Task Pc10BlockWriteAsync(int address32, byte[] dataBytes, CancellationToken cancellationToken = default)
```

##### Pc10MultiReadAsync

```csharp
public Task<byte[]> Pc10MultiReadAsync(byte[] payload, CancellationToken cancellationToken = default)
```

##### Pc10MultiWriteAsync

```csharp
public Task Pc10MultiWriteAsync(byte[] payload, CancellationToken cancellationToken = default)
```

##### ReadFrWordsAsync

```csharp
public Task<int[]> ReadFrWordsAsync(int index, int count, CancellationToken cancellationToken = default)
```

##### WriteFrWordsAsync

```csharp
public Task WriteFrWordsAsync(int index, IEnumerable<int> values, bool commit = false, CancellationToken cancellationToken = default)
```

##### WriteFrWordsExAsync

```csharp
public Task WriteFrWordsExAsync(int index, IEnumerable<int> values, bool commit = false, bool wait = false, double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### CommitFrBlockAsync

```csharp
public Task<CpuStatusData> CommitFrBlockAsync(int index, bool wait = false, double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### CommitFrRangeAsync

```csharp
public Task<CpuStatusData> CommitFrRangeAsync(int index, int count = 1, bool wait = false, double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### WriteFrWordsCommittedAsync

```csharp
public Task WriteFrWordsCommittedAsync(int index, IEnumerable<int> values, CancellationToken cancellationToken = default)
```

##### FrRegisterAsync

```csharp
public Task FrRegisterAsync(int exNo, CancellationToken cancellationToken = default)
```

##### RelayCommandAsync

```csharp
public Task<ResponseFrame> RelayCommandAsync(int linkNo, int stationNo, byte[] innerPayload, CancellationToken cancellationToken = default)
```

##### RelayNestedAsync

```csharp
public Task<ResponseFrame> RelayNestedAsync(IEnumerable<ValueTuple<int, int>> hops, byte[] innerPayload, CancellationToken cancellationToken = default)
```

##### SendViaRelayAsync

```csharp
public Task<ResponseFrame> SendViaRelayAsync(object hops, byte[] innerPayload, CancellationToken cancellationToken = default)
```

##### RelayReadWordsAsync

```csharp
public Task<int[]> RelayReadWordsAsync(object hops, int address, int count, CancellationToken cancellationToken = default)
```

##### RelayWriteWordsAsync

```csharp
public Task RelayWriteWordsAsync(object hops, int address, IEnumerable<int> values, CancellationToken cancellationToken = default)
```

##### RelayReadClockAsync

```csharp
public Task<ClockData> RelayReadClockAsync(object hops, CancellationToken cancellationToken = default)
```

##### RelayWriteClockAsync

```csharp
public Task RelayWriteClockAsync(object hops, DateTime value, CancellationToken cancellationToken = default)
```

##### RelayResumeScanAsync

```csharp
public Task RelayResumeScanAsync(object hops, CancellationToken cancellationToken = default)
```

##### RelayStopScanAsync

```csharp
public Task RelayStopScanAsync(object hops, CancellationToken cancellationToken = default)
```

##### RelayReleaseScanStopAsync

```csharp
public Task RelayReleaseScanStopAsync(object hops, CancellationToken cancellationToken = default)
```

##### RelayReadCpuStatusAsync

```csharp
public Task<CpuStatusData> RelayReadCpuStatusAsync(object hops, CancellationToken cancellationToken = default)
```

##### RelayReadCpuStatusA0RawAsync

```csharp
public Task<byte[]> RelayReadCpuStatusA0RawAsync(object hops, CancellationToken cancellationToken = default)
```

##### RelayReadCpuStatusA0Async

```csharp
public Task<CpuStatusData> RelayReadCpuStatusA0Async(object hops, CancellationToken cancellationToken = default)
```

##### RelayWriteFrWordsAsync

```csharp
public Task RelayWriteFrWordsAsync(object hops, int index, IEnumerable<int> values, bool commit = false, CancellationToken cancellationToken = default)
```

##### RelayWriteFrWordsExAsync

```csharp
public Task RelayWriteFrWordsExAsync(object hops, int index, IEnumerable<int> values, bool commit = false, bool wait = false, double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### RelayFrRegisterAsync

```csharp
public Task RelayFrRegisterAsync(object hops, int exNo, CancellationToken cancellationToken = default)
```

##### RelayCommitFrBlockAsync

```csharp
public Task<CpuStatusData> RelayCommitFrBlockAsync(object hops, int index, bool wait = false, double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### RelayCommitFrRangeAsync

```csharp
public Task<CpuStatusData> RelayCommitFrRangeAsync(object hops, int index, int count = 1, bool wait = false, double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### RelayWaitFrWriteCompleteAsync

```csharp
public Task<CpuStatusData> RelayWaitFrWriteCompleteAsync(object hops, double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### ReadClockAsync

```csharp
public Task<ClockData> ReadClockAsync(CancellationToken cancellationToken = default)
```

##### ReadCpuStatusAsync

```csharp
public Task<CpuStatusData> ReadCpuStatusAsync(CancellationToken cancellationToken = default)
```

##### ReadCpuStatusA0RawAsync

```csharp
public Task<byte[]> ReadCpuStatusA0RawAsync(CancellationToken cancellationToken = default)
```

##### ReadCpuStatusA0Async

```csharp
public Task<CpuStatusData> ReadCpuStatusA0Async(CancellationToken cancellationToken = default)
```

##### WaitFrWriteCompleteAsync

```csharp
public Task<CpuStatusData> WaitFrWriteCompleteAsync(double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### WriteClockAsync

```csharp
public Task WriteClockAsync(DateTime value, CancellationToken cancellationToken = default)
```

##### ResumeScanAsync

```csharp
public Task ResumeScanAsync(CancellationToken cancellationToken = default)
```

##### StopScanAsync

```csharp
public Task StopScanAsync(CancellationToken cancellationToken = default)
```

##### ReleaseScanStopAsync

```csharp
public Task ReleaseScanStopAsync(CancellationToken cancellationToken = default)
```

##### ToyopucClient

```csharp
public ToyopucClient(string host, int port, int localPort = 0, ToyopucTransportMode transport = Tcp, TimeSpan timeout = default, int retries = 0, TimeSpan retryDelay = default, int recvBufsize = 65535)
```

##### Open

```csharp
public virtual void Open()
```

##### Close

```csharp
public virtual void Close()
```

##### Dispose

```csharp
public void Dispose()
```

##### ClearTraceFrames

```csharp
public void ClearTraceFrames()
```

##### SendRaw

```csharp
public ResponseFrame SendRaw(int cmd, byte[] data = null)
```

##### SendPayload

```csharp
public ResponseFrame SendPayload(byte[] payload)
```

##### ReadWords

```csharp
public int[] ReadWords(int address, int count)
```

##### WriteWords

```csharp
public void WriteWords(int address, IEnumerable<int> values)
```

##### ReadBytes

```csharp
public byte[] ReadBytes(int address, int count)
```

##### WriteBytes

```csharp
public void WriteBytes(int address, IEnumerable<int> values)
```

##### ReadBit

```csharp
public bool ReadBit(int address)
```

##### WriteBit

```csharp
public void WriteBit(int address, bool value)
```

##### ReadWordsMulti

```csharp
public int[] ReadWordsMulti(IEnumerable<int> addresses)
```

##### WriteWordsMulti

```csharp
public void WriteWordsMulti(IEnumerable<ValueTuple<int, int>> pairs)
```

##### ReadBytesMulti

```csharp
public byte[] ReadBytesMulti(IEnumerable<int> addresses)
```

##### WriteBytesMulti

```csharp
public void WriteBytesMulti(IEnumerable<ValueTuple<int, int>> pairs)
```

##### ReadExtWords

```csharp
public int[] ReadExtWords(int number, int address, int count)
```

##### WriteExtWords

```csharp
public void WriteExtWords(int number, int address, IEnumerable<int> values)
```

##### ReadExtBytes

```csharp
public byte[] ReadExtBytes(int number, int address, int count)
```

##### WriteExtBytes

```csharp
public void WriteExtBytes(int number, int address, IEnumerable<int> values)
```

##### ReadExtMulti

```csharp
public byte[] ReadExtMulti(IEnumerable<ValueTuple<int, int, int>> bitPoints, IEnumerable<ValueTuple<int, int>> bytePoints, IEnumerable<ValueTuple<int, int>> wordPoints)
```

##### WriteExtMulti

```csharp
public void WriteExtMulti(IEnumerable<ValueTuple<int, int, int, int>> bitPoints, IEnumerable<ValueTuple<int, int, int>> bytePoints, IEnumerable<ValueTuple<int, int, int>> wordPoints)
```

##### Pc10BlockRead

```csharp
public byte[] Pc10BlockRead(int address32, int count)
```

##### Pc10BlockWrite

```csharp
public void Pc10BlockWrite(int address32, byte[] dataBytes)
```

##### Pc10MultiRead

```csharp
public byte[] Pc10MultiRead(byte[] payload)
```

##### Pc10MultiWrite

```csharp
public void Pc10MultiWrite(byte[] payload)
```

##### ReadFrWords

```csharp
public int[] ReadFrWords(int index, int count)
```

##### WriteFrWords

```csharp
public void WriteFrWords(int index, IEnumerable<int> values, bool commit = false)
```

##### WriteFrWordsEx

```csharp
public void WriteFrWordsEx(int index, IEnumerable<int> values, bool commit = false, bool wait = false, double timeout = 30, double pollInterval = 0.2)
```

##### CommitFrBlock

```csharp
public CpuStatusData CommitFrBlock(int index, bool wait = false, double timeout = 30, double pollInterval = 0.2)
```

##### CommitFrRange

```csharp
public CpuStatusData CommitFrRange(int index, int count = 1, bool wait = false, double timeout = 30, double pollInterval = 0.2)
```

##### WriteFrWordsCommitted

```csharp
public void WriteFrWordsCommitted(int index, IEnumerable<int> values)
```

##### FrRegister

```csharp
public void FrRegister(int exNo)
```

##### RelayCommand

```csharp
public ResponseFrame RelayCommand(int linkNo, int stationNo, byte[] innerPayload)
```

##### RelayNested

```csharp
public ResponseFrame RelayNested(IEnumerable<ValueTuple<int, int>> hops, byte[] innerPayload)
```

##### SendViaRelay

```csharp
public ResponseFrame SendViaRelay(object hops, byte[] innerPayload)
```

##### RelayReadWords

```csharp
public int[] RelayReadWords(object hops, int address, int count)
```

##### RelayWriteWords

```csharp
public void RelayWriteWords(object hops, int address, IEnumerable<int> values)
```

##### RelayReadClock

```csharp
public ClockData RelayReadClock(object hops)
```

##### RelayWriteClock

```csharp
public void RelayWriteClock(object hops, DateTime value)
```

##### RelayResumeScan

```csharp
public void RelayResumeScan(object hops)
```

##### RelayStopScan

```csharp
public void RelayStopScan(object hops)
```

##### RelayReleaseScanStop

```csharp
public void RelayReleaseScanStop(object hops)
```

##### RelayReadCpuStatus

```csharp
public CpuStatusData RelayReadCpuStatus(object hops)
```

##### RelayReadCpuStatusA0Raw

```csharp
public byte[] RelayReadCpuStatusA0Raw(object hops)
```

##### RelayReadCpuStatusA0

```csharp
public CpuStatusData RelayReadCpuStatusA0(object hops)
```

##### RelayWriteFrWords

```csharp
public void RelayWriteFrWords(object hops, int index, IEnumerable<int> values, bool commit = false)
```

##### RelayWriteFrWordsEx

```csharp
public void RelayWriteFrWordsEx(object hops, int index, IEnumerable<int> values, bool commit = false, bool wait = false, double timeout = 30, double pollInterval = 0.2)
```

##### RelayFrRegister

```csharp
public void RelayFrRegister(object hops, int exNo)
```

##### RelayCommitFrBlock

```csharp
public CpuStatusData RelayCommitFrBlock(object hops, int index, bool wait = false, double timeout = 30, double pollInterval = 0.2)
```

##### RelayCommitFrRange

```csharp
public CpuStatusData RelayCommitFrRange(object hops, int index, int count = 1, bool wait = false, double timeout = 30, double pollInterval = 0.2)
```

##### RelayWaitFrWriteComplete

```csharp
public CpuStatusData RelayWaitFrWriteComplete(object hops, double timeout = 30, double pollInterval = 0.2)
```

##### ReadClock

```csharp
public ClockData ReadClock()
```

##### ReadCpuStatus

```csharp
public CpuStatusData ReadCpuStatus()
```

##### ReadCpuStatusA0Raw

```csharp
public byte[] ReadCpuStatusA0Raw()
```

##### ReadCpuStatusA0

```csharp
public CpuStatusData ReadCpuStatusA0()
```

##### WaitFrWriteComplete

```csharp
public CpuStatusData WaitFrWriteComplete(double timeout = 30, double pollInterval = 0.2)
```

##### WriteClock

```csharp
public void WriteClock(DateTime value)
```

##### ResumeScan

```csharp
public void ResumeScan()
```

##### StopScan

```csharp
public void StopScan()
```

##### ReleaseScanStop

```csharp
public void ReleaseScanStop()
```

##### Host

```csharp
public string Host { get; }
```

##### Port

```csharp
public int Port { get; }
```

##### LocalPort

```csharp
public int LocalPort { get; }
```

##### Transport

```csharp
public ToyopucTransportMode Transport { get; }
```

##### Timeout

```csharp
public TimeSpan Timeout { get; set; }
```

##### Retries

```csharp
public int Retries { get; }
```

##### RetryDelay

```csharp
public TimeSpan RetryDelay { get; }
```

##### RecvBufsize

```csharp
public int RecvBufsize { get; }
```

##### IsOpen

```csharp
public bool IsOpen { get; }
```

##### CaptureTraceFrames

```csharp
public bool CaptureTraceFrames { get; set; }
```

##### TraceHook

```csharp
public Action<ToyopucTraceFrame> TraceHook { get; set; }
```

##### LastTx

```csharp
public byte[] LastTx { get; }
```

##### LastRx

```csharp
public byte[] LastRx { get; }
```

##### TraceFrames

```csharp
public IReadOnlyList<TransportTraceFrame> TraceFrames { get; }
```

### ToyopucConnectionOptions

```csharp
public sealed class ToyopucConnectionOptions
```

Explicit connection options for a stable TOYOPUC device session.

Remarks: This type keeps transport, profile, retry, and relay settings explicit for the unified high-level connection flow and generated API documentation.

#### Members

##### ToyopucConnectionOptions

```csharp
public ToyopucConnectionOptions(string Host)
```

Explicit connection options for a stable TOYOPUC device session.

Remarks: This type keeps transport, profile, retry, and relay settings explicit for the unified high-level connection flow and generated API documentation.

Parameters:
- `Host`: PLC IP address or hostname.

##### Host

```csharp
public string Host { get; set; }
```

PLC IP address or hostname.

##### Port

```csharp
public int Port { get; set; }
```

Gets or sets the PLC port number.

Remarks: The default TOYOPUC communication port is `1025`.

##### Timeout

```csharp
public TimeSpan Timeout { get; set; }
```

Gets or sets the communication timeout.

Remarks: A zero value falls back to `EffectiveTimeout`.

##### Transport

```csharp
public ToyopucTransportMode Transport { get; set; }
```

Gets or sets the transport protocol.

##### PlcProfile

```csharp
public string PlcProfile { get; set; }
```

Gets or sets the optional PLC profile name.

Remarks: Supply a known profile name when you want documented profile-based address resolution instead of a fully generic session.

##### RelayHops

```csharp
public string RelayHops { get; set; }
```

Gets or sets the optional relay hop chain text.

Remarks: Leave this empty for direct connections; provide relay hops for routed sessions.

##### LocalPort

```csharp
public int LocalPort { get; set; }
```

Gets or sets the local UDP port. Ignored for TCP.

##### Retries

```csharp
public int Retries { get; set; }
```

Gets or sets the retry count for transport operations.

##### RetryDelay

```csharp
public TimeSpan RetryDelay { get; set; }
```

Gets or sets the retry delay.

##### RecvBufsize

```csharp
public int RecvBufsize { get; set; }
```

Gets or sets the receive buffer size.

##### EffectiveTimeout

```csharp
public TimeSpan EffectiveTimeout { get; }
```

Gets the effective timeout used for a new client instance.

##### EffectiveRetryDelay

```csharp
public TimeSpan EffectiveRetryDelay { get; }
```

Gets the effective retry delay used for a new client instance.

### ToyopucDeviceCatalog

```csharp
public static class ToyopucDeviceCatalog
```

#### Members

##### GetAreaDescriptors

```csharp
public static IReadOnlyList<ToyopucAreaDescriptor> GetAreaDescriptors(string profile = null)
```

##### GetAreas

```csharp
public static IReadOnlyList<string> GetAreas(bool prefixed, string profile = null)
```

##### GetAreaDescriptor

```csharp
public static ToyopucAreaDescriptor GetAreaDescriptor(string area, string profile = null)
```

##### GetSupportedRanges

```csharp
public static IReadOnlyList<ToyopucAddressRange> GetSupportedRanges(string area, bool prefixed, string profile = null)
```

##### GetSupportedRanges

```csharp
public static IReadOnlyList<ToyopucAddressRange> GetSupportedRanges(string area, bool prefixed, bool packed, string profile = null)
```

##### GetSupportedRanges

```csharp
public static IReadOnlyList<ToyopucAddressRange> GetSupportedRanges(string area, bool prefixed, string unit, bool packed = false, string profile = null)
```

##### FormatAddressRange

```csharp
public static string FormatAddressRange(string familyCode, ToyopucAddressRange range, int width)
```

##### FormatAddressRanges

```csharp
public static string FormatAddressRanges(string familyCode, IEnumerable<ToyopucAddressRange> ranges, int width)
```

##### GetSupportedRange

```csharp
public static ToyopucAddressRange GetSupportedRange(string area, bool prefixed, string profile = null)
```

##### GetSupportedRange

```csharp
public static ToyopucAddressRange GetSupportedRange(string area, bool prefixed, bool packed, string profile = null)
```

##### GetSupportedRange

```csharp
public static ToyopucAddressRange GetSupportedRange(string area, bool prefixed, string unit, bool packed = false, string profile = null)
```

##### IsSupportedIndex

```csharp
public static bool IsSupportedIndex(string area, int index, bool prefixed, string profile = null)
```

##### IsSupportedIndex

```csharp
public static bool IsSupportedIndex(string area, int index, bool prefixed, bool packed, string profile = null)
```

##### IsSupportedIndex

```csharp
public static bool IsSupportedIndex(string area, int index, bool prefixed, string unit, bool packed = false, string profile = null)
```

##### GetSuggestedStartAddresses

```csharp
public static IReadOnlyList<string> GetSuggestedStartAddresses(string area, string prefix = null, string profile = null)
```

##### GetSuggestedStartAddresses

```csharp
public static IReadOnlyList<string> GetSuggestedStartAddresses(string area, ToyopucAddressingOptions options)
```

##### GetSuggestedStartAddresses

```csharp
public static IReadOnlyList<string> GetSuggestedStartAddresses(string area, string prefix, ToyopucAddressingOptions options)
```

##### GetSuggestedStartAddresses

```csharp
public static IReadOnlyList<string> GetSuggestedStartAddresses(string area, string prefix, string unit, bool packed, string profile)
```

### ToyopucDeviceClient

```csharp
public class ToyopucDeviceClient
```

#### Members

##### ReadDWord

```csharp
public uint ReadDWord(object device)
```

##### WriteDWord

```csharp
public void WriteDWord(object device, uint value)
```

##### ReadDWords

```csharp
public uint[] ReadDWords(object device, int count, bool atomicTransfer = false)
```

##### WriteDWords

```csharp
public void WriteDWords(object device, IEnumerable<uint> values, bool atomicTransfer = false)
```

##### ReadFloat32

```csharp
public float ReadFloat32(object device)
```

##### WriteFloat32

```csharp
public void WriteFloat32(object device, float value)
```

##### ReadFloat32s

```csharp
public float[] ReadFloat32s(object device, int count, bool atomicTransfer = false)
```

##### WriteFloat32s

```csharp
public void WriteFloat32s(object device, IEnumerable<float> values, bool atomicTransfer = false)
```

##### RelayReadDWord

```csharp
public uint RelayReadDWord(object hops, object device)
```

##### RelayWriteDWord

```csharp
public void RelayWriteDWord(object hops, object device, uint value)
```

##### RelayReadDWords

```csharp
public uint[] RelayReadDWords(object hops, object device, int count, bool atomicTransfer = false)
```

##### RelayWriteDWords

```csharp
public void RelayWriteDWords(object hops, object device, IEnumerable<uint> values, bool atomicTransfer = false)
```

##### RelayReadFloat32

```csharp
public float RelayReadFloat32(object hops, object device)
```

##### RelayWriteFloat32

```csharp
public void RelayWriteFloat32(object hops, object device, float value)
```

##### RelayReadFloat32s

```csharp
public float[] RelayReadFloat32s(object hops, object device, int count, bool atomicTransfer = false)
```

##### RelayWriteFloat32s

```csharp
public void RelayWriteFloat32s(object hops, object device, IEnumerable<float> values, bool atomicTransfer = false)
```

##### ResolveDeviceAsync

```csharp
public Task<ResolvedDevice> ResolveDeviceAsync(string device, CancellationToken cancellationToken = default)
```

##### RelayReadAsync

```csharp
public Task<object> RelayReadAsync(object hops, object device, int count = 1, CancellationToken cancellationToken = default)
```

##### RelayWriteAsync

```csharp
public Task RelayWriteAsync(object hops, object device, object value, CancellationToken cancellationToken = default)
```

##### RelayReadWordsAsync

```csharp
public Task<object> RelayReadWordsAsync(object hops, object device, int count = 1, CancellationToken cancellationToken = default)
```

##### RelayWriteWordsAsync

```csharp
public Task RelayWriteWordsAsync(object hops, object device, object value, CancellationToken cancellationToken = default)
```

##### RelayReadManyAsync

```csharp
public Task<object[]> RelayReadManyAsync(object hops, IEnumerable<object> devices, CancellationToken cancellationToken = default)
```

##### RelayWriteManyAsync

```csharp
public Task RelayWriteManyAsync(object hops, IEnumerable<KeyValuePair<object, object>> items, CancellationToken cancellationToken = default)
```

##### ReadFrAsync

```csharp
public Task<object> ReadFrAsync(object device, int count = 1, CancellationToken cancellationToken = default)
```

##### RelayReadFrAsync

```csharp
public Task<object> RelayReadFrAsync(object hops, object device, int count = 1, CancellationToken cancellationToken = default)
```

##### WriteFrAsync

```csharp
public Task WriteFrAsync(object device, object value, bool commit = false, bool? wait = null, double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### RelayWriteFrAsync

```csharp
public Task RelayWriteFrAsync(object hops, object device, object value, bool commit = false, bool? wait = null, double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### CommitFrAsync

```csharp
public Task CommitFrAsync(object device, int count = 1, bool wait = false, double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### RelayCommitFrAsync

```csharp
public Task RelayCommitFrAsync(object hops, object device, int count = 1, bool wait = false, double timeout = 30, double pollInterval = 0.2, CancellationToken cancellationToken = default)
```

##### ReadAsync

```csharp
public Task<object> ReadAsync(object device, int count = 1, CancellationToken cancellationToken = default)
```

##### WriteAsync

```csharp
public Task WriteAsync(object device, object value, CancellationToken cancellationToken = default)
```

##### ReadManyAsync

```csharp
public Task<object[]> ReadManyAsync(IEnumerable<object> devices, CancellationToken cancellationToken = default)
```

##### WriteManyAsync

```csharp
public Task WriteManyAsync(IEnumerable<KeyValuePair<object, object>> items, CancellationToken cancellationToken = default)
```

##### ReadDWordAsync

```csharp
public Task<uint> ReadDWordAsync(object device, CancellationToken cancellationToken = default)
```

##### WriteDWordAsync

```csharp
public Task WriteDWordAsync(object device, uint value, CancellationToken cancellationToken = default)
```

##### ReadDWordsAsync

```csharp
public Task<uint[]> ReadDWordsAsync(object device, int count, bool atomicTransfer = false, CancellationToken cancellationToken = default)
```

##### WriteDWordsAsync

```csharp
public Task WriteDWordsAsync(object device, IEnumerable<uint> values, bool atomicTransfer = false, CancellationToken cancellationToken = default)
```

##### ReadFloat32Async

```csharp
public Task<float> ReadFloat32Async(object device, CancellationToken cancellationToken = default)
```

##### WriteFloat32Async

```csharp
public Task WriteFloat32Async(object device, float value, CancellationToken cancellationToken = default)
```

##### ReadFloat32sAsync

```csharp
public Task<float[]> ReadFloat32sAsync(object device, int count, bool atomicTransfer = false, CancellationToken cancellationToken = default)
```

##### WriteFloat32sAsync

```csharp
public Task WriteFloat32sAsync(object device, IEnumerable<float> values, bool atomicTransfer = false, CancellationToken cancellationToken = default)
```

##### RelayReadDWordAsync

```csharp
public Task<uint> RelayReadDWordAsync(object hops, object device, CancellationToken cancellationToken = default)
```

##### RelayWriteDWordAsync

```csharp
public Task RelayWriteDWordAsync(object hops, object device, uint value, CancellationToken cancellationToken = default)
```

##### RelayReadDWordsAsync

```csharp
public Task<uint[]> RelayReadDWordsAsync(object hops, object device, int count, bool atomicTransfer = false, CancellationToken cancellationToken = default)
```

##### RelayWriteDWordsAsync

```csharp
public Task RelayWriteDWordsAsync(object hops, object device, IEnumerable<uint> values, bool atomicTransfer = false, CancellationToken cancellationToken = default)
```

##### RelayReadFloat32Async

```csharp
public Task<float> RelayReadFloat32Async(object hops, object device, CancellationToken cancellationToken = default)
```

##### RelayWriteFloat32Async

```csharp
public Task RelayWriteFloat32Async(object hops, object device, float value, CancellationToken cancellationToken = default)
```

##### RelayReadFloat32sAsync

```csharp
public Task<float[]> RelayReadFloat32sAsync(object hops, object device, int count, bool atomicTransfer = false, CancellationToken cancellationToken = default)
```

##### RelayWriteFloat32sAsync

```csharp
public Task RelayWriteFloat32sAsync(object hops, object device, IEnumerable<float> values, bool atomicTransfer = false, CancellationToken cancellationToken = default)
```

##### ToyopucDeviceClient

```csharp
public ToyopucDeviceClient(string host, int port, int localPort = 0, ToyopucTransportMode transport = Tcp, TimeSpan timeout = default, int retries = 0, TimeSpan retryDelay = default, int recvBufsize = 65535, ToyopucAddressingOptions addressingOptions = null, string plcProfile = null)
```

##### ResolveDevice

```csharp
public ResolvedDevice ResolveDevice(string device)
```

##### RelayRead

```csharp
public object RelayRead(object hops, object device, int count = 1)
```

##### RelayWrite

```csharp
public void RelayWrite(object hops, object device, object value)
```

##### RelayReadWords

```csharp
public object RelayReadWords(object hops, object device, int count = 1)
```

##### RelayWriteWords

```csharp
public void RelayWriteWords(object hops, object device, object value)
```

##### RelayReadMany

```csharp
public object[] RelayReadMany(object hops, IEnumerable<object> devices)
```

##### RelayWriteMany

```csharp
public void RelayWriteMany(object hops, IEnumerable<KeyValuePair<object, object>> items)
```

##### ReadFr

```csharp
public object ReadFr(object device, int count = 1)
```

##### RelayReadFr

```csharp
public object RelayReadFr(object hops, object device, int count = 1)
```

##### WriteFr

```csharp
public void WriteFr(object device, object value, bool commit = false, bool? wait = null, double timeout = 30, double pollInterval = 0.2)
```

##### RelayWriteFr

```csharp
public void RelayWriteFr(object hops, object device, object value, bool commit = false, bool? wait = null, double timeout = 30, double pollInterval = 0.2)
```

##### CommitFr

```csharp
public void CommitFr(object device, int count = 1, bool wait = false, double timeout = 30, double pollInterval = 0.2)
```

##### RelayCommitFr

```csharp
public void RelayCommitFr(object hops, object device, int count = 1, bool wait = false, double timeout = 30, double pollInterval = 0.2)
```

##### Read

```csharp
public object Read(object device, int count = 1)
```

##### Write

```csharp
public void Write(object device, object value)
```

##### ReadMany

```csharp
public object[] ReadMany(IEnumerable<object> devices)
```

##### WriteMany

```csharp
public void WriteMany(IEnumerable<KeyValuePair<object, object>> items)
```

##### AddressingOptions

```csharp
public ToyopucAddressingOptions AddressingOptions { get; }
```

##### PlcProfile

```csharp
public string PlcProfile { get; }
```

### ToyopucDeviceClientExtensions

```csharp
public static class ToyopucDeviceClientExtensions
```

#### Members

##### ReadTypedAsync

```csharp
public static Task<object> ReadTypedAsync(ToyopucDeviceClient client, string device, string dtype, CancellationToken ct = default)
```

##### ReadTypedAsync

```csharp
public static Task<object> ReadTypedAsync(QueuedToyopucDeviceClient client, string device, string dtype, CancellationToken ct = default)
```

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync(ToyopucDeviceClient client, string device, string dtype, object value, CancellationToken ct = default)
```

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync(QueuedToyopucDeviceClient client, string device, string dtype, object value, CancellationToken ct = default)
```

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(ToyopucDeviceClient client, string device, int bitIndex, bool value, CancellationToken ct = default)
```

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(QueuedToyopucDeviceClient client, string device, int bitIndex, bool value, CancellationToken ct = default)
```

##### ReadNamedAsync

```csharp
public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(ToyopucDeviceClient client, IEnumerable<string> addresses, CancellationToken ct = default)
```

##### ReadNamedAsync

```csharp
public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(QueuedToyopucDeviceClient client, IEnumerable<string> addresses, CancellationToken ct = default)
```

##### PollAsync

```csharp
public static IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(ToyopucDeviceClient client, IEnumerable<string> addresses, TimeSpan interval, CancellationToken ct = default)
```

##### PollAsync

```csharp
public static IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(QueuedToyopucDeviceClient client, IEnumerable<string> addresses, TimeSpan interval, CancellationToken ct = default)
```

##### ReadWordsAsync

```csharp
public static Task<ushort[]> ReadWordsAsync(ToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
```

##### ReadWordsAsync

```csharp
public static Task<ushort[]> ReadWordsAsync(QueuedToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
```

##### ReadDWordsAsync

```csharp
public static Task<uint[]> ReadDWordsAsync(ToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
```

##### ReadDWordsAsync

```csharp
public static Task<uint[]> ReadDWordsAsync(QueuedToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
```

##### ReadWordsSingleRequestAsync

```csharp
public static Task<ushort[]> ReadWordsSingleRequestAsync(ToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
```

##### ReadWordsSingleRequestAsync

```csharp
public static Task<ushort[]> ReadWordsSingleRequestAsync(QueuedToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
```

##### ReadDWordsSingleRequestAsync

```csharp
public static Task<uint[]> ReadDWordsSingleRequestAsync(ToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
```

##### ReadDWordsSingleRequestAsync

```csharp
public static Task<uint[]> ReadDWordsSingleRequestAsync(QueuedToyopucDeviceClient client, string device, int count, CancellationToken ct = default)
```

##### WriteWordsSingleRequestAsync

```csharp
public static Task WriteWordsSingleRequestAsync(ToyopucDeviceClient client, string device, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

##### WriteWordsSingleRequestAsync

```csharp
public static Task WriteWordsSingleRequestAsync(QueuedToyopucDeviceClient client, string device, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

##### WriteDWordsSingleRequestAsync

```csharp
public static Task WriteDWordsSingleRequestAsync(ToyopucDeviceClient client, string device, IReadOnlyList<uint> values, CancellationToken ct = default)
```

##### WriteDWordsSingleRequestAsync

```csharp
public static Task WriteDWordsSingleRequestAsync(QueuedToyopucDeviceClient client, string device, IReadOnlyList<uint> values, CancellationToken ct = default)
```

##### ReadWordsChunkedAsync

```csharp
public static Task<ushort[]> ReadWordsChunkedAsync(ToyopucDeviceClient client, string device, int count, int maxWordsPerRequest, CancellationToken ct = default)
```

##### ReadWordsChunkedAsync

```csharp
public static Task<ushort[]> ReadWordsChunkedAsync(QueuedToyopucDeviceClient client, string device, int count, int maxWordsPerRequest, CancellationToken ct = default)
```

##### ReadDWordsChunkedAsync

```csharp
public static Task<uint[]> ReadDWordsChunkedAsync(ToyopucDeviceClient client, string device, int count, int maxDwordsPerRequest, CancellationToken ct = default)
```

##### ReadDWordsChunkedAsync

```csharp
public static Task<uint[]> ReadDWordsChunkedAsync(QueuedToyopucDeviceClient client, string device, int count, int maxDwordsPerRequest, CancellationToken ct = default)
```

##### WriteWordsChunkedAsync

```csharp
public static Task WriteWordsChunkedAsync(ToyopucDeviceClient client, string device, IReadOnlyList<ushort> values, int maxWordsPerRequest, CancellationToken ct = default)
```

##### WriteWordsChunkedAsync

```csharp
public static Task WriteWordsChunkedAsync(QueuedToyopucDeviceClient client, string device, IReadOnlyList<ushort> values, int maxWordsPerRequest, CancellationToken ct = default)
```

##### WriteDWordsChunkedAsync

```csharp
public static Task WriteDWordsChunkedAsync(ToyopucDeviceClient client, string device, IReadOnlyList<uint> values, int maxDwordsPerRequest, CancellationToken ct = default)
```

##### WriteDWordsChunkedAsync

```csharp
public static Task WriteDWordsChunkedAsync(QueuedToyopucDeviceClient client, string device, IReadOnlyList<uint> values, int maxDwordsPerRequest, CancellationToken ct = default)
```

##### OpenAndConnectAsync

```csharp
public static Task<QueuedToyopucDeviceClient> OpenAndConnectAsync(ToyopucConnectionOptions options, CancellationToken ct = default)
```

##### OpenAndConnectAsync

```csharp
public static Task<QueuedToyopucDeviceClient> OpenAndConnectAsync(string host, int port = 1025, CancellationToken ct = default)
```

### ToyopucDeviceClientFactory

```csharp
public static class ToyopucDeviceClientFactory
```

Factory helpers for creating connected queued TOYOPUC clients.

Remarks: This factory is the preferred application entry point when you want explicit profile, relay, retry, and timeout settings captured in one documented type.

#### Members

##### OpenAndConnectAsync

```csharp
public static Task<QueuedToyopucDeviceClient> OpenAndConnectAsync(ToyopucConnectionOptions options, CancellationToken cancellationToken = default)
```

Creates, configures, and opens a queued TOYOPUC client.

Remarks: When `RelayHops` is supplied, the returned queued client keeps the normalized relay chain available through `RelayHops`.

Returns: A connected queued client.

Parameters:
- `options`: Explicit connection options.
- `cancellationToken`: Cancellation token.

### ToyopucDeviceResolver

```csharp
public static class ToyopucDeviceResolver
```

#### Members

##### ResolveDevice

```csharp
public static ResolvedDevice ResolveDevice(string device, ToyopucAddressingOptions options = null, string profile = null)
```

### ToyopucError

```csharp
public class ToyopucError
```

#### Members

##### ToyopucError

```csharp
public ToyopucError()
```

##### ToyopucError

```csharp
public ToyopucError(string message)
```

##### ToyopucError

```csharp
public ToyopucError(string message, Exception innerException)
```

### ToyopucPlcProfile

```csharp
public sealed class ToyopucPlcProfile
```

#### Members

##### ToyopucPlcProfile

```csharp
public ToyopucPlcProfile(string Name, ToyopucAddressingOptions AddressingOptions, IReadOnlyList<ToyopucAreaDescriptor> Areas)
```

##### Name

```csharp
public string Name { get; set; }
```

##### AddressingOptions

```csharp
public ToyopucAddressingOptions AddressingOptions { get; set; }
```

##### Areas

```csharp
public IReadOnlyList<ToyopucAreaDescriptor> Areas { get; set; }
```

### ToyopucPlcProfiles

```csharp
public static class ToyopucPlcProfiles
```

#### Members

##### GetNames

```csharp
public static IReadOnlyList<string> GetNames()
```

##### NormalizeName

```csharp
public static string NormalizeName(string profile)
```

##### FromName

```csharp
public static ToyopucPlcProfile FromName(string profile)
```

##### Generic

```csharp
public static ToyopucPlcProfile Generic { get; }
```

##### ToyopucPlusStandard

```csharp
public static ToyopucPlcProfile ToyopucPlusStandard { get; }
```

##### ToyopucPlusExtended

```csharp
public static ToyopucPlcProfile ToyopucPlusExtended { get; }
```

##### Nano10GxMode

```csharp
public static ToyopucPlcProfile Nano10GxMode { get; }
```

##### Nano10GxCompatible

```csharp
public static ToyopucPlcProfile Nano10GxCompatible { get; }
```

##### Pc10GStandardPc3Jg

```csharp
public static ToyopucPlcProfile Pc10GStandardPc3Jg { get; }
```

##### Pc10GMode

```csharp
public static ToyopucPlcProfile Pc10GMode { get; }
```

##### Pc3JxPc3Separate

```csharp
public static ToyopucPlcProfile Pc3JxPc3Separate { get; }
```

##### Pc3JxPlusExpansion

```csharp
public static ToyopucPlcProfile Pc3JxPlusExpansion { get; }
```

##### Pc3JgMode

```csharp
public static ToyopucPlcProfile Pc3JgMode { get; }
```

##### Pc3JgPc3Separate

```csharp
public static ToyopucPlcProfile Pc3JgPc3Separate { get; }
```

### ToyopucProtocol

```csharp
public static class ToyopucProtocol
```

#### Members

##### FtCommand

```csharp
public const byte FtCommand
```

##### FtResponse

```csharp
public const byte FtResponse
```

##### BuildCommand

```csharp
public static byte[] BuildCommand(int cmd, byte[] data = null)
```

##### ParseResponse

```csharp
public static ResponseFrame ParseResponse(byte[] frame)
```

##### PackU16LittleEndian

```csharp
public static byte[] PackU16LittleEndian(int value)
```

##### UnpackU16LittleEndian

```csharp
public static int[] UnpackU16LittleEndian(byte[] data)
```

##### PackExtBitSpec

```csharp
public static int PackExtBitSpec(int number, int bit)
```

##### PackBcd

```csharp
public static int PackBcd(int value)
```

##### UnpackBcd

```csharp
public static int UnpackBcd(int value)
```

##### BuildClockRead

```csharp
public static byte[] BuildClockRead()
```

##### BuildCpuStatusRead

```csharp
public static byte[] BuildCpuStatusRead()
```

##### BuildCpuStatusReadA0

```csharp
public static byte[] BuildCpuStatusReadA0()
```

##### BuildScanResume

```csharp
public static byte[] BuildScanResume()
```

##### BuildScanStop

```csharp
public static byte[] BuildScanStop()
```

##### BuildScanStopRelease

```csharp
public static byte[] BuildScanStopRelease()
```

##### BuildClockWrite

```csharp
public static byte[] BuildClockWrite(int second, int minute, int hour, int day, int month, int year2Digit, int weekday)
```

##### ParseClockData

```csharp
public static ClockData ParseClockData(byte[] data)
```

##### ParseCpuStatusData

```csharp
public static CpuStatusData ParseCpuStatusData(byte[] data)
```

##### ParseCpuStatusDataA0

```csharp
public static CpuStatusData ParseCpuStatusDataA0(byte[] data)
```

##### ParseCpuStatusDataA0Raw

```csharp
public static byte[] ParseCpuStatusDataA0Raw(byte[] data)
```

##### BuildWordRead

```csharp
public static byte[] BuildWordRead(int address, int count)
```

##### BuildWordWrite

```csharp
public static byte[] BuildWordWrite(int address, IEnumerable<int> values)
```

##### BuildByteRead

```csharp
public static byte[] BuildByteRead(int address, int count)
```

##### BuildByteWrite

```csharp
public static byte[] BuildByteWrite(int address, IEnumerable<int> values)
```

##### BuildBitRead

```csharp
public static byte[] BuildBitRead(int address)
```

##### BuildBitWrite

```csharp
public static byte[] BuildBitWrite(int address, int value)
```

##### BuildMultiWordRead

```csharp
public static byte[] BuildMultiWordRead(IEnumerable<int> addresses)
```

##### BuildMultiWordWrite

```csharp
public static byte[] BuildMultiWordWrite(IEnumerable<ValueTuple<int, int>> pairs)
```

##### BuildMultiByteRead

```csharp
public static byte[] BuildMultiByteRead(IEnumerable<int> addresses)
```

##### BuildMultiByteWrite

```csharp
public static byte[] BuildMultiByteWrite(IEnumerable<ValueTuple<int, int>> pairs)
```

##### BuildExtWordRead

```csharp
public static byte[] BuildExtWordRead(int number, int address, int count)
```

##### BuildExtWordWrite

```csharp
public static byte[] BuildExtWordWrite(int number, int address, IEnumerable<int> values)
```

##### BuildExtByteRead

```csharp
public static byte[] BuildExtByteRead(int number, int address, int count)
```

##### BuildExtByteWrite

```csharp
public static byte[] BuildExtByteWrite(int number, int address, IEnumerable<int> values)
```

##### BuildExtMultiRead

```csharp
public static byte[] BuildExtMultiRead(IEnumerable<ValueTuple<int, int, int>> bitPoints, IEnumerable<ValueTuple<int, int>> bytePoints, IEnumerable<ValueTuple<int, int>> wordPoints)
```

##### BuildExtMultiWrite

```csharp
public static byte[] BuildExtMultiWrite(IEnumerable<ValueTuple<int, int, int, int>> bitPoints, IEnumerable<ValueTuple<int, int, int>> bytePoints, IEnumerable<ValueTuple<int, int, int>> wordPoints)
```

##### BuildPc10BlockRead

```csharp
public static byte[] BuildPc10BlockRead(int address32, int count)
```

##### BuildPc10BlockWrite

```csharp
public static byte[] BuildPc10BlockWrite(int address32, byte[] dataBytes)
```

##### BuildPc10MultiRead

```csharp
public static byte[] BuildPc10MultiRead(byte[] payload)
```

##### BuildPc10MultiWrite

```csharp
public static byte[] BuildPc10MultiWrite(byte[] payload)
```

##### BuildFrRegister

```csharp
public static byte[] BuildFrRegister(int exNo)
```

##### BuildRelayCommand

```csharp
public static byte[] BuildRelayCommand(int linkNo, int stationNo, byte[] innerPayload, int enq = 5)
```

##### BuildRelayNested

```csharp
public static byte[] BuildRelayNested(IEnumerable<ValueTuple<int, int>> hops, byte[] innerPayload)
```

### ToyopucProtocolError

```csharp
public class ToyopucProtocolError
```

#### Members

##### ToyopucProtocolError

```csharp
public ToyopucProtocolError()
```

##### ToyopucProtocolError

```csharp
public ToyopucProtocolError(string message)
```

##### ToyopucProtocolError

```csharp
public ToyopucProtocolError(string message, Exception innerException)
```

### ToyopucRelay

```csharp
public static class ToyopucRelay
```

#### Members

##### ParseRelayHops

```csharp
public static IReadOnlyList<ValueTuple<int, int>> ParseRelayHops(string text)
```

##### NormalizeRelayHops

```csharp
public static IReadOnlyList<ValueTuple<int, int>> NormalizeRelayHops(object hops)
```

##### NormalizeRelayHops

```csharp
public static IReadOnlyList<ValueTuple<int, int>> NormalizeRelayHops(IEnumerable<ValueTuple<int, int>> hops)
```

##### FormatRelayHop

```csharp
public static string FormatRelayHop(int linkNo, int stationNo)
```

##### ParseRelayInnerResponse

```csharp
public static ValueTuple<ResponseFrame, byte[]> ParseRelayInnerResponse(byte[] innerRaw)
```

##### UnwrapRelayResponseChain

```csharp
public static ValueTuple<IReadOnlyList<RelayLayer>, ResponseFrame> UnwrapRelayResponseChain(ResponseFrame response)
```

### ToyopucTimeoutError

```csharp
public class ToyopucTimeoutError
```

#### Members

##### ToyopucTimeoutError

```csharp
public ToyopucTimeoutError()
```

##### ToyopucTimeoutError

```csharp
public ToyopucTimeoutError(string message)
```

##### ToyopucTimeoutError

```csharp
public ToyopucTimeoutError(string message, Exception innerException)
```

### ToyopucTraceDirection

```csharp
public enum ToyopucTraceDirection
```

#### Members

##### Send

```csharp
public const ToyopucTraceDirection Send
```

##### Receive

```csharp
public const ToyopucTraceDirection Receive
```

### ToyopucTraceFrame

```csharp
public sealed class ToyopucTraceFrame
```

#### Members

##### ToyopucTraceFrame

```csharp
public ToyopucTraceFrame(ToyopucTraceDirection Direction, byte[] Data, DateTime Timestamp)
```

##### Direction

```csharp
public ToyopucTraceDirection Direction { get; set; }
```

##### Data

```csharp
public byte[] Data { get; set; }
```

##### Timestamp

```csharp
public DateTime Timestamp { get; set; }
```

### ToyopucTransportMode

```csharp
public enum ToyopucTransportMode
```

#### Members

##### Tcp

```csharp
public const ToyopucTransportMode Tcp
```

##### Udp

```csharp
public const ToyopucTransportMode Udp
```

### TransportTraceFrame

```csharp
public sealed class TransportTraceFrame
```

#### Members

##### TransportTraceFrame

```csharp
public TransportTraceFrame(byte[] Tx, byte[] Rx)
```

##### Tx

```csharp
public byte[] Tx { get; set; }
```

##### Rx

```csharp
public byte[] Rx { get; set; }
```
