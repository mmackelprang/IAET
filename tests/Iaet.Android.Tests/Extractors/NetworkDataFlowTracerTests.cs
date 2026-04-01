using FluentAssertions;
using Iaet.Android.Extractors;
using Iaet.Core.Models;

namespace Iaet.Android.Tests.Extractors;

public sealed class NetworkDataFlowTracerTests
{
    [Fact]
    public void Trace_detects_gRPC_onNext_callback()
    {
        var java = """
            public class MessageObserver implements StreamObserver<MessageProto> {
                @Override
                public void onNext(MessageProto response) {
                    String text = response.getText();
                    adapter.notifyDataSetChanged();
                }
            }
            """;

        var results = NetworkDataFlowTracer.Trace(java, "MessageObserver.java");

        results.Should().ContainSingle();
        results[0].SourceType.Should().Be("gRPC");
        results[0].InferredPurpose.Should().Be("messaging/SMS data");
    }

    [Fact]
    public void Trace_detects_Retrofit_onResponse_callback()
    {
        var java = """
            public class ContactLoader {
                public void load() {
                    call.enqueue(new Callback<ContactResponse>() {
                        @Override
                        public void onResponse(Call<ContactResponse> call, Response<ContactResponse> response) {
                            ContactResponse body = response.body();
                            contactList.postValue(body.getContacts());
                        }
                    });
                }
            }
            """;

        var results = NetworkDataFlowTracer.Trace(java, "ContactLoader.java");

        // Should detect both Retrofit pattern (onResponse with Response) and possibly OkHttp
        results.Should().NotBeEmpty();
        results.Should().Contain(r => r.SourceType == "Retrofit" || r.SourceType == "OkHttp");
    }

    [Fact]
    public void Trace_detects_OkHttp_onResponse_callback()
    {
        var java = """
            public class HttpClient {
                public void fetch() {
                    client.newCall(request).enqueue(new Callback() {
                        @Override
                        public void onResponse(Call call, Response response) {
                            String json = response.body().string();
                            JSONObject obj = new JSONObject(json);
                        }
                    });
                }
            }
            """;

        var results = NetworkDataFlowTracer.Trace(java, "HttpClient.java");

        results.Should().NotBeEmpty();
        results.Should().Contain(r => r.SourceType == "OkHttp");
    }

    [Fact]
    public void Trace_with_LiveData_postValue_finds_UI_binding()
    {
        var java = """
            public class DataFetcher {
                private MutableLiveData<List<Item>> items;

                public void onNext(ItemListResponse resp) {
                    List<Item> parsed = resp.getItemsList();
                    items.postValue(parsed);
                }
            }
            """;

        var results = NetworkDataFlowTracer.Trace(java, "DataFetcher.java");

        results.Should().NotBeEmpty();
        var flow = results[0];
        flow.UiBinding.Should().Contain("postValue");
        flow.TargetVariable.Should().Be("items");
    }

    [Fact]
    public void Trace_infers_purpose_from_filename_call()
    {
        var java = """
            public class CallHistoryObserver implements StreamObserver<CallRecord> {
                @Override
                public void onNext(CallRecord record) {
                    adapter.notifyDataSetChanged();
                }
            }
            """;

        var results = NetworkDataFlowTracer.Trace(java, "CallHistoryObserver.java");

        results.Should().ContainSingle();
        results[0].InferredPurpose.Should().Be("call/VoIP data");
    }

    [Fact]
    public void Trace_infers_purpose_from_filename_settings()
    {
        var java = """
            public class SettingsLoader {
                public void onNext(SettingsProto proto) {
                    profileName.setText(proto.getName());
                }
            }
            """;

        var results = NetworkDataFlowTracer.Trace(java, "AccountSettingsLoader.java");

        results.Should().NotBeEmpty();
        results[0].InferredPurpose.Should().Be("account/settings data");
    }

    [Fact]
    public void Trace_with_empty_input_returns_empty()
    {
        var results = NetworkDataFlowTracer.Trace("", "empty.java");

        results.Should().BeEmpty();
    }

    [Fact]
    public void Trace_with_null_input_returns_empty()
    {
        var results = NetworkDataFlowTracer.Trace(null!, "file.java");

        results.Should().BeEmpty();
    }

    [Fact]
    public void Trace_with_response_body_pattern()
    {
        var java = """
            public class ApiClient {
                public void fetchData() {
                    String data = response.body().string();
                    JSONObject json = new JSONObject(data);
                    titleView.setText(json.getString("title"));
                }
            }
            """;

        var results = NetworkDataFlowTracer.Trace(java, "ApiClient.java");

        results.Should().NotBeEmpty();
        results[0].SourceType.Should().Be("HTTP");
        results[0].ParsingDescription.Should().Contain("JSONObject");
    }

    [Fact]
    public void Trace_with_protobuf_parseFrom()
    {
        var java = """
            public class ProtoHandler implements StreamObserver<byte[]> {
                @Override
                public void onNext(byte[] data) {
                    MyProto proto = MyProto.parseFrom(data);
                    String name = proto.getName();
                    nameView.setText(name);
                }
            }
            """;

        var results = NetworkDataFlowTracer.Trace(java, "ProtoHandler.java");

        results.Should().NotBeEmpty();
        results[0].SourceType.Should().Be("gRPC");
        results[0].ParsingDescription.Should().Contain("parseFrom");
    }

    [Fact]
    public void Trace_high_confidence_when_parsing_and_ui_present()
    {
        var java = """
            public class FullFlow {
                @Override
                public void onNext(DataProto data) {
                    String parsed = data.getName();
                    label.setText(parsed);
                }
            }
            """;

        var results = NetworkDataFlowTracer.Trace(java, "FullFlow.java");

        results.Should().NotBeEmpty();
        results[0].Confidence.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void Trace_low_confidence_when_no_parsing_no_ui()
    {
        var java = """
            public class MinimalHandler {
                @Override
                public void onNext(Object obj) {
                    log(obj);
                }
            }
            """;

        var results = NetworkDataFlowTracer.Trace(java, "MinimalHandler.java");

        results.Should().NotBeEmpty();
        results[0].Confidence.Should().Be(ConfidenceLevel.Low);
    }

    [Fact]
    public void TraceFromDirectory_returns_empty_for_nonexistent_directory()
    {
        var results = NetworkDataFlowTracer.TraceFromDirectory("/nonexistent/path");

        results.Should().BeEmpty();
    }

    [Fact]
    public void TraceFromDirectory_throws_on_null_directory()
    {
        var act = () => NetworkDataFlowTracer.TraceFromDirectory(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TraceFromDirectory_scans_java_files()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iaet-net-flow-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Observer.java"), """
                public class Observer implements StreamObserver<Proto> {
                    @Override
                    public void onNext(Proto data) {
                        label.setText(data.getValue());
                    }
                }
                """);
            File.WriteAllText(Path.Combine(tempDir, "Utils.java"), """
                public class Utils {
                    public static int clamp(int x, int a, int b) { return x; }
                }
                """);

            var results = NetworkDataFlowTracer.TraceFromDirectory(tempDir);

            results.Should().ContainSingle();
            results[0].SourceFile.Should().Contain("Observer.java");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Trace_infers_notification_purpose()
    {
        var java = """
            public class AlertService {
                public void onNext(AlertPayload alert) {
                    notifyView.setText(alert.getTitle());
                }
            }
            """;

        var results = NetworkDataFlowTracer.Trace(java, "NotificationService.java");

        results.Should().NotBeEmpty();
        results[0].InferredPurpose.Should().Be("notification data");
    }

    [Fact]
    public void Trace_truncates_long_response_handler()
    {
        var longParams = new string('x', 100);
        var java = "public class LongHandler {\n" +
                   "    public void onNext(" + longParams + " data) {\n" +
                   "        doSomething();\n" +
                   "    }\n" +
                   "}\n";

        var results = NetworkDataFlowTracer.Trace(java, "LongHandler.java");

        results.Should().NotBeEmpty();
        results[0].ResponseHandler!.Length.Should().BeLessThanOrEqualTo(80);
    }
}
