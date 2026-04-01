using FluentAssertions;
using Iaet.Android.Extractors;

namespace Iaet.Android.Tests.Extractors;

public sealed class ProtoFieldMapperTests
{
    [Fact]
    public void Extract_finds_field_number_constants()
    {
        var java = """
            public class AccountInfo {
                public static final int PHONE_NUMBER_FIELD_NUMBER = 1;
                public static final int DEVICES_FIELD_NUMBER = 3;
                public static final int BILLING_FIELD_NUMBER = 5;
            }
            """;

        var mappings = ProtoFieldMapper.Extract(java, "AccountInfo.java");

        mappings.Should().Contain(m => m.Position == 0 && m.SuggestedName == "phoneNumber"); // 1-based -> 0-based
        mappings.Should().Contain(m => m.Position == 2 && m.SuggestedName == "devices");
        mappings.Should().Contain(m => m.Position == 4 && m.SuggestedName == "billing");
    }

    [Fact]
    public void Extract_finds_getter_methods_with_nearby_position()
    {
        var java = """
            public String getPhoneNumber() {
                return (String) this.data.get(0);
            }
            public List getDevicesList() {
                return (List) this.data.get(2);
            }
            """;

        var mappings = ProtoFieldMapper.Extract(java, "Account.java");

        mappings.Should().Contain(m => m.Position == 0 && m.SuggestedName == "phoneNumber");
        mappings.Should().Contain(m => m.Position == 2 && m.SuggestedName == "devicesList");
    }

    [Fact]
    public void Extract_finds_positional_access_with_assignment()
    {
        var java = """
            String wsUrl = response.get(2);
            int port = config.get(3);
            """;

        var mappings = ProtoFieldMapper.Extract(java, "SipConfig.java");

        mappings.Should().Contain(m => m.Position == 2 && m.SuggestedName == "wsUrl");
        mappings.Should().Contain(m => m.Position == 3 && m.SuggestedName == "port");
    }

    [Fact]
    public void Extract_finds_proto_descriptors()
    {
        var java = """
            private static final String PROTO = "google.voice.v1.AccountInfo";
            private static final String FILE = "sipregisterinfo.proto";
            """;

        var mappings = ProtoFieldMapper.Extract(java, "Proto.java");

        mappings.Should().Contain(m => m.Source == "descriptor" && m.SuggestedName == "google.voice.v1.AccountInfo");
        mappings.Should().Contain(m => m.Source == "descriptor" && m.SuggestedName == "sipregisterinfo.proto");
    }

    [Fact]
    public void Extract_handles_obfuscated_single_letter_vars()
    {
        var java = """
            a = b.get(0);
            this.c = d.get(1);
            """;

        var mappings = ProtoFieldMapper.Extract(java, "obfuscated.java");

        // Single letter without this. should not create high confidence mapping
        // But this.c should be captured
        mappings.Should().Contain(m => m.Position == 1);
    }

    [Fact]
    public void Extract_converts_field_constant_names_correctly()
    {
        var java = """
            public static final int WEB_SOCKET_URL_FIELD_NUMBER = 7;
            public static final int IS_ACTIVE_FIELD_NUMBER = 2;
            """;

        var mappings = ProtoFieldMapper.Extract(java, "Config.java");

        mappings.Should().Contain(m => m.SuggestedName == "webSocketUrl");
        mappings.Should().Contain(m => m.SuggestedName == "isActive");
    }

    [Fact]
    public void Extract_handles_empty_input()
    {
        ProtoFieldMapper.Extract("", "empty.java").Should().BeEmpty();
    }

    [Fact]
    public void ExtractFromDirectory_scans_java_files()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "iaet-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Proto.java"),
                """public static final int NAME_FIELD_NUMBER = 1;""");

            var mappings = ProtoFieldMapper.ExtractFromDirectory(tempDir);
            mappings.Should().Contain(m => m.SuggestedName == "name");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ExtractFromDirectory_handles_missing_directory()
    {
        ProtoFieldMapper.ExtractFromDirectory("/nonexistent/path").Should().BeEmpty();
    }
}
