using Umbraco.Cms.Core.Packaging;

namespace Neirijnck.Transfr.Migrations;

/// <summary>
/// The package's migrations, discovered by Umbraco and run unattended on boot (PackageMigrationsUnattended, default on).
/// Each step's key is permanent: Umbraco stores the last completed one and runs only the steps after it.
/// </summary>
public sealed class TransfrMigrationPlan : PackageMigrationPlan
{
    public TransfrMigrationPlan()
        : base("Neirijnck.Transfr", "Transfr", "Transfr")
    {
    }

    protected override void DefinePlan()
        => this.To<GrantSectionToAdministrators>(new Guid("8c1f3c2e-5a7d-4f2b-9e61-3d0b7a4c2f18"))
            .To<CreateCopyHistory>(new Guid("efe6f7b8-0399-448e-8e64-7e44c133526a"))
            .To<AddCopyHistoryKind>(new Guid("e3ebeac5-196d-4250-9003-e5a6efcfc5aa"));
}