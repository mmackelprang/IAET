using Iaet.Core.Models;

namespace Iaet.ProtocolAnalysis;

public interface IStreamAnalyzer
{
    bool CanAnalyze(StreamProtocol protocol);
    StreamAnalysis Analyze(CapturedStream stream);
}
