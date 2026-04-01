using FluentAssertions;
using Iaet.Android.Extractors;
using Iaet.Core.Models;
using Iaet.Diagrams;

namespace Iaet.Diagrams.Tests;

public sealed class DataFlowDiagramGeneratorTests
{
    // ── Network flows ─────────────────────────────────────────────────────────

    [Fact]
    public void GenerateFromNetworkFlows_produces_flowchart_LR()
    {
        var flows = new List<NetworkDataFlow>
        {
            MakeNetworkFlow("gRPC", "MessageObserver.java"),
        };

        var mermaid = DataFlowDiagramGenerator.GenerateFromNetworkFlows("Network Test", flows);

        mermaid.Should().StartWith("flowchart LR");
    }

    [Fact]
    public void GenerateFromNetworkFlows_empty_flows_produces_no_data_node()
    {
        var mermaid = DataFlowDiagramGenerator.GenerateFromNetworkFlows("Empty", []);

        mermaid.Should().Contain("flowchart LR");
        mermaid.Should().Contain("No data flows traced");
    }

    [Fact]
    public void GenerateFromNetworkFlows_source_type_appears_in_diagram()
    {
        var flows = new List<NetworkDataFlow>
        {
            MakeNetworkFlow("Retrofit", "ContactLoader.java"),
        };

        var mermaid = DataFlowDiagramGenerator.GenerateFromNetworkFlows("Source type", flows);

        mermaid.Should().Contain("Retrofit Response");
    }

    [Fact]
    public void GenerateFromNetworkFlows_ui_binding_appears_in_diagram()
    {
        var flows = new List<NetworkDataFlow>
        {
            MakeNetworkFlow("gRPC", "DataFetcher.java", uiBinding: "items.postValue()"),
        };

        var mermaid = DataFlowDiagramGenerator.GenerateFromNetworkFlows("UI binding", flows);

        mermaid.Should().Contain("items.postValue()");
        mermaid.Should().Contain("_ui");
    }

    [Fact]
    public void GenerateFromNetworkFlows_parsing_description_appears_in_diagram()
    {
        var flows = new List<NetworkDataFlow>
        {
            MakeNetworkFlow("OkHttp", "HttpClient.java", parsing: "JSONObject"),
        };

        var mermaid = DataFlowDiagramGenerator.GenerateFromNetworkFlows("Parsing", flows);

        mermaid.Should().Contain("JSONObject");
        mermaid.Should().Contain("_parse");
    }

    [Fact]
    public void GenerateFromNetworkFlows_long_source_file_is_truncated()
    {
        var longFile = new string('a', 40) + ".java";
        var flows = new List<NetworkDataFlow>
        {
            MakeNetworkFlow("gRPC", longFile),
        };

        var mermaid = DataFlowDiagramGenerator.GenerateFromNetworkFlows("Truncation", flows);

        // The label shown should start with "..." due to truncation
        mermaid.Should().Contain("...");
    }

    [Fact]
    public void GenerateFromNetworkFlows_inferred_purpose_appears_as_comment()
    {
        var flows = new List<NetworkDataFlow>
        {
            MakeNetworkFlow("gRPC", "MessageService.java", inferredPurpose: "messaging/SMS data"),
        };

        var mermaid = DataFlowDiagramGenerator.GenerateFromNetworkFlows("Purpose", flows);

        mermaid.Should().Contain("%% Purpose: messaging/SMS data");
    }

    [Fact]
    public void GenerateFromNetworkFlows_multiple_flows_produce_unique_node_ids()
    {
        var flows = new List<NetworkDataFlow>
        {
            MakeNetworkFlow("gRPC", "Flow1.java"),
            MakeNetworkFlow("Retrofit", "Flow2.java"),
        };

        var mermaid = DataFlowDiagramGenerator.GenerateFromNetworkFlows("Multi", flows);

        mermaid.Should().Contain("F0_src");
        mermaid.Should().Contain("F1_src");
    }

    // ── BLE flows ─────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateFromBleFlows_produces_flowchart_LR()
    {
        var flows = new List<BleDataFlow>
        {
            MakeBleFlow(callbackLocation: "HeartRateGatt.java:42"),
        };

        var mermaid = DataFlowDiagramGenerator.GenerateFromBleFlows("BLE Test", flows);

        mermaid.Should().StartWith("flowchart LR");
    }

    [Fact]
    public void GenerateFromBleFlows_empty_flows_produces_no_data_node()
    {
        var mermaid = DataFlowDiagramGenerator.GenerateFromBleFlows("Empty BLE", []);

        mermaid.Should().Contain("flowchart LR");
        mermaid.Should().Contain("No BLE data flows traced");
    }

    [Fact]
    public void GenerateFromBleFlows_characteristic_node_always_present()
    {
        var flows = new List<BleDataFlow>
        {
            MakeBleFlow(),
        };

        var mermaid = DataFlowDiagramGenerator.GenerateFromBleFlows("BLE char", flows);

        mermaid.Should().Contain("BLE Characteristic");
        mermaid.Should().Contain("B0_char");
    }

    [Fact]
    public void GenerateFromBleFlows_ui_binding_appears_in_diagram()
    {
        var flows = new List<BleDataFlow>
        {
            MakeBleFlow(uiBinding: "heartRate via LiveData.postValue"),
        };

        var mermaid = DataFlowDiagramGenerator.GenerateFromBleFlows("BLE UI", flows);

        mermaid.Should().Contain("heartRate via LiveData.postValue");
        mermaid.Should().Contain("_ui");
    }

    [Fact]
    public void GenerateFromBleFlows_inferred_meaning_appears_as_comment()
    {
        var flows = new List<BleDataFlow>
        {
            MakeBleFlow(inferredMeaning: "heart rate data"),
        };

        var mermaid = DataFlowDiagramGenerator.GenerateFromBleFlows("BLE infer", flows);

        mermaid.Should().Contain("%% Inferred: heart rate data");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static NetworkDataFlow MakeNetworkFlow(
        string sourceType,
        string sourceFile,
        string? parsing = null,
        string? uiBinding = null,
        string? inferredPurpose = null) => new()
        {
            SourceType = sourceType,
            SourceFile = sourceFile,
            ParsingDescription = parsing,
            UiBinding = uiBinding,
            InferredPurpose = inferredPurpose,
            Confidence = ConfidenceLevel.Medium,
        };

    private static BleDataFlow MakeBleFlow(
        string? callbackLocation = null,
        string? parsing = null,
        string? uiBinding = null,
        string? inferredMeaning = null) => new()
        {
            CharacteristicUuid = "00002a37-0000-1000-8000-00805f9b34fb",
            CallbackLocation = callbackLocation,
            ParsingDescription = parsing,
            UiBinding = uiBinding,
            InferredMeaning = inferredMeaning,
            Confidence = ConfidenceLevel.Medium,
        };
}
