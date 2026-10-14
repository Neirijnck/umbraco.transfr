using Neirijnck.Transfr.Compare;
using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Models;

/// <summary>
/// Core results the tree compare and the copy plan both answer with.
/// </summary>
internal static class ResponseMapping
{
    public static SchemaProblemResponseModel From(SchemaProblem problem)
        => new(problem.Kind.ToString(), problem.Key, problem.Name, problem.IsElement, problem.Reason.ToString(), problem.Details);

    public static ContentDifferenceResponseModel From(ContentDifference difference)
        => new(difference.Kind.ToString(), difference.Alias, difference.Culture, difference.Segment);

    public static SeparateInstallResponseModel From(SeparateInstallEvidence evidence)
        => new(evidence.Alias, evidence.Name, evidence.LocalKey, evidence.RemoteKey);
}