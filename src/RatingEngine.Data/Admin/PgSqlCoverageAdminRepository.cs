using System.Data;
using Dapper;
using RatingEngine.Core;

namespace RatingEngine.Data.Admin;

/// <summary>
/// PostgreSQL implementation of <see cref="ICoverageAdminRepository"/>.
/// All SQL uses quoted PascalCase identifiers and PostgreSQL dialect
/// (RETURNING "Id" instead of OUTPUT INSERTED.Id, NOW() instead of GETUTCDATE()).
/// </summary>
public sealed class PgSqlCoverageAdminRepository : ICoverageAdminRepository
{
    private readonly DbConnectionFactory _db;
    public PgSqlCoverageAdminRepository(DbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<CoverageSummary>> ListAsync(int? coverageRefId = null, int? productManifestId = null, CancellationToken cancellationToken = default)
    {
        string sql;
        object param;

        if (coverageRefId.HasValue)
        {
            sql = """
                SELECT cc."Id", cc."CoverageRefId", pm."ProductCode", cc."State", cr."CoverageCode",
                       cc."Version", cc."EffStart", cc."ExpireAt", cc."CreatedAt", cc."CreatedBy"
                FROM "CoverageConfig" cc
                JOIN "CoverageRef" cr     ON cr."Id"  = cc."CoverageRefId"
                JOIN "Product" pm ON pm."Id"  = cr."ProductId"
                WHERE cc."CoverageRefId" = @CoverageRefId
                ORDER BY cc."State", cc."Version"
                """;
            param = new { CoverageRefId = coverageRefId };
        }
        else if (productManifestId.HasValue)
        {
            sql = """
                SELECT cc."Id", cc."CoverageRefId", pm."ProductCode", cc."State", cr."CoverageCode",
                       cc."Version", cc."EffStart", cc."ExpireAt", cc."CreatedAt", cc."CreatedBy"
                FROM "CoverageConfig" cc
                JOIN "CoverageRef" cr     ON cr."Id"  = cc."CoverageRefId"
                JOIN "Product" pm ON pm."Id"  = cr."ProductId"
                WHERE cr."ProductId" = @ProductManifestId
                ORDER BY cr."CoverageCode", cc."State", cc."Version"
                """;
            param = new { ProductManifestId = productManifestId };
        }
        else
        {
            sql = """
                SELECT cc."Id", cc."CoverageRefId", pm."ProductCode", cc."State", cr."CoverageCode",
                       cc."Version", cc."EffStart", cc."ExpireAt", cc."CreatedAt", cc."CreatedBy"
                FROM "CoverageConfig" cc
                JOIN "CoverageRef" cr     ON cr."Id"  = cc."CoverageRefId"
                JOIN "Product" pm ON pm."Id"  = cr."ProductId"
                ORDER BY pm."ProductCode", cr."CoverageCode", cc."State", cc."Version"
                """;
            param = new { };
        }

        using var conn = _db.Create();
        return (await conn.QueryAsync<CoverageSummary>(new CommandDefinition(sql, param, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<CoverageDetail?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        const string configSql = """
            SELECT cc."Id", cc."CoverageRefId", pm."ProductCode", cc."State", cr."CoverageCode",
                   cc."Version", cc."EffStart", cc."ExpireAt",
                   cc."CreatedAt", cc."CreatedBy", cc."ModifiedAt", cc."ModifiedBy", cc."Notes"
            FROM "CoverageConfig" cc
            JOIN "CoverageRef" cr     ON cr."Id"  = cc."CoverageRefId"
            JOIN "Product" pm ON pm."Id"  = cr."ProductId"
            WHERE cc."Id" = @Id
            """;
        const string perilSql   = """SELECT "PerilCode" FROM "CoveragePeril" WHERE "CoverageConfigId" = @ConfigId ORDER BY "SortOrder" """;
        const string stepSql    = """
            SELECT "Id", "StepOrder", "StepId", "Name", "Operation",
                   "RateTableName", "MathType", "InterpolateKey", "RangeKeyName",
                   "ComputeExpr", "ComputeStoreAs", "ComputeApplyToPremium",
                   "RoundPrecision", "RoundMode",
                   "WhenPath", "WhenOperator", "WhenValue"
            FROM "PipelineStep" WHERE "CoverageConfigId" = @ConfigId ORDER BY "StepOrder"
            """;
        const string stepKeySql = """SELECT "PipelineStepId", "KeyName", "KeyValue" FROM "PipelineStepKey" WHERE "PipelineStepId" IN @StepIds """;
        const string stepWhenSql = """
            SELECT "PipelineStepId", "GroupId", "ClausePath", "ClauseOp", "ClauseValue", "SortOrder"
            FROM "PipelineStepWhenClause" WHERE "PipelineStepId" IN @StepIds
            ORDER BY "PipelineStepId", "GroupId", "SortOrder"
            """;
        const string depSql     = """SELECT "DependsOnCode" FROM "CoverageDependency" WHERE "CoverageConfigId" = @ConfigId ORDER BY "SortOrder" """;
        const string publishSql = """SELECT "PublishKey" FROM "CoveragePublish" WHERE "CoverageConfigId" = @ConfigId ORDER BY "SortOrder" """;
        const string aggSql     = """
            SELECT ac."Id", ac."WhenPath", ac."WhenOp", ac."WhenValue",
                   af."Id" AS "FieldId", af."SourceField", af."AggFunction", af."ResultKey", af."SortOrder" AS "FieldSort"
            FROM "CoverageAggregateConfig" ac
            LEFT JOIN "CoverageAggregateField" af ON af."CoverageAggregateConfigId" = ac."Id"
            WHERE ac."CoverageConfigId" = @ConfigId
            ORDER BY af."SortOrder"
            """;

        using var conn = _db.Create();

        var row = await conn.QueryFirstOrDefaultAsync<CoverageRow>(new CommandDefinition(configSql, new { Id = id }, cancellationToken: cancellationToken));
        if (row is null) return null;

        var perils      = (await conn.QueryAsync<string>(new CommandDefinition(perilSql, new { ConfigId = row.Id }, cancellationToken: cancellationToken))).AsList();
        var stepRows    = (await conn.QueryAsync<StepRow>(new CommandDefinition(stepSql, new { ConfigId = row.Id }, cancellationToken: cancellationToken))).AsList();
        var dependsOn   = (await conn.QueryAsync<string>(new CommandDefinition(depSql, new { ConfigId = row.Id }, cancellationToken: cancellationToken))).AsList();
        var publishKeys = (await conn.QueryAsync<string>(new CommandDefinition(publishSql, new { ConfigId = row.Id }, cancellationToken: cancellationToken))).AsList();
        var aggRows     = (await conn.QueryAsync<AggRow>(new CommandDefinition(aggSql, new { ConfigId = row.Id }, cancellationToken: cancellationToken))).AsList();

        List<StepKeyRow>  keyRows  = [];
        List<SqlCoverageAdminRepository.WhenClauseRow> whenRows = [];
        if (stepRows.Count > 0)
        {
            var stepIds = stepRows.Select(s => s.Id).ToArray();
            keyRows  = (await conn.QueryAsync<StepKeyRow>(new CommandDefinition(stepKeySql, new { StepIds = stepIds }, cancellationToken: cancellationToken))).AsList();
            whenRows = (await conn.QueryAsync<SqlCoverageAdminRepository.WhenClauseRow>(new CommandDefinition(stepWhenSql, new { StepIds = stepIds }, cancellationToken: cancellationToken))).AsList();
        }

        var keysByStep = keyRows .GroupBy(k => k.PipelineStepId).ToDictionary(g => g.Key, g => g.ToDictionary(k => k.KeyName, k => k.KeyValue));
        var whenByStep = whenRows.GroupBy(w => w.PipelineStepId).ToDictionary(g => g.Key, g => g.ToList());
        var pipeline   = stepRows.Select(s => BuildStep(s, keysByStep, whenByStep)).ToList();

        AggregateConfigDetail? aggregate = null;
        if (aggRows.Count > 0 && aggRows[0].Id > 0)
        {
            var first = aggRows[0];
            var fields = aggRows.Where(a => a.FieldId.HasValue)
                .Select(a => new AggregateFieldDetail(a.FieldId!.Value, a.SourceField!, a.AggFunction!, a.ResultKey!, a.FieldSort)).ToList();
            aggregate = new AggregateConfigDetail(first.Id, first.WhenPath, first.WhenOp, first.WhenValue, fields);
        }

        return new CoverageDetail(
            row.Id, row.CoverageRefId, row.ProductCode, row.State, row.CoverageCode, row.Version,
            row.EffStart, row.ExpireAt, row.CreatedAt, row.CreatedBy,
            row.ModifiedAt, row.ModifiedBy, row.Notes, perils, pipeline)
        {
            DependsOn = dependsOn, Publish = publishKeys, Aggregate = aggregate,
        };
    }

    public async Task<int> CreateAsync(CreateCoverageRequest req, string? actor = null, CancellationToken cancellationToken = default)
    {
        const string insertSql = """
            INSERT INTO "CoverageConfig" ("CoverageRefId", "State", "Version", "EffStart", "ExpireAt", "Notes", "CreatedAt", "CreatedBy")
            VALUES (@CoverageRefId, @State, @Version, @EffStart, @ExpireAt, @Notes, NOW(), @Actor)
            RETURNING "Id"
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var id = await conn.QuerySingleAsync<int>(new CommandDefinition(
            insertSql,
            new { req.CoverageRefId, req.State, req.Version, req.EffStart, req.ExpireAt, req.Notes, Actor = actor },
            transaction: tx, cancellationToken: cancellationToken));

        await InsertPerilsAsync(conn, tx, id, req.Perils, cancellationToken);
        await InsertPipelineAsync(conn, tx, id, req.Pipeline, cancellationToken);
        await InsertDependenciesAsync(conn, tx, id, req.DependsOn, cancellationToken);
        await InsertPublishKeysAsync(conn, tx, id, req.Publish, cancellationToken);
        if (req.Aggregate is not null) await InsertAggregateConfigAsync(conn, tx, id, req.Aggregate, cancellationToken);

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, UpdateCoverageRequest req, string? actor = null, CancellationToken cancellationToken = default)
    {
        const string updateSql = """
            UPDATE "CoverageConfig"
            SET "EffStart" = @EffStart, "ExpireAt" = @ExpireAt, "Notes" = @Notes,
                "ModifiedAt" = NOW(), "ModifiedBy" = @Actor
            WHERE "Id" = @Id
            """;

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            updateSql, new { Id = id, req.EffStart, req.ExpireAt, req.Notes, Actor = actor },
            transaction: tx, cancellationToken: cancellationToken));

        if (affected == 0) { tx.Rollback(); return false; }

        await conn.ExecuteAsync(new CommandDefinition("""DELETE FROM "CoveragePeril"          WHERE "CoverageConfigId" = @Id""", new { Id = id }, transaction: tx, cancellationToken: cancellationToken));
        await conn.ExecuteAsync(new CommandDefinition("""DELETE FROM "PipelineStep"            WHERE "CoverageConfigId" = @Id""", new { Id = id }, transaction: tx, cancellationToken: cancellationToken));
        await conn.ExecuteAsync(new CommandDefinition("""DELETE FROM "CoverageDependency"      WHERE "CoverageConfigId" = @Id""", new { Id = id }, transaction: tx, cancellationToken: cancellationToken));
        await conn.ExecuteAsync(new CommandDefinition("""DELETE FROM "CoveragePublish"         WHERE "CoverageConfigId" = @Id""", new { Id = id }, transaction: tx, cancellationToken: cancellationToken));
        await conn.ExecuteAsync(new CommandDefinition("""DELETE FROM "CoverageAggregateConfig" WHERE "CoverageConfigId" = @Id""", new { Id = id }, transaction: tx, cancellationToken: cancellationToken));

        await InsertPerilsAsync(conn, tx, id, req.Perils, cancellationToken);
        await InsertPipelineAsync(conn, tx, id, req.Pipeline, cancellationToken);
        await InsertDependenciesAsync(conn, tx, id, req.DependsOn, cancellationToken);
        await InsertPublishKeysAsync(conn, tx, id, req.Publish, cancellationToken);
        if (req.Aggregate is not null) await InsertAggregateConfigAsync(conn, tx, id, req.Aggregate, cancellationToken);

        tx.Commit();
        return true;
    }

    public async Task<bool> ExpireAsync(int id, DateOnly expireAt, string? actor = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "CoverageConfig"
            SET "ExpireAt" = @ExpireAt, "ModifiedAt" = NOW(), "ModifiedBy" = @Actor
            WHERE "Id" = @Id
            """;
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(sql,
            new { Id = id, ExpireAt = expireAt, Actor = actor }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var conn = _db.Create();
        return await conn.ExecuteAsync(new CommandDefinition(
            """DELETE FROM "CoverageConfig" WHERE "Id" = @Id""",
            new { Id = id }, cancellationToken: cancellationToken)) > 0;
    }

    // ── Helpers shared with PgSqlPipelineStepAdminRepository ──────────────────

    internal static async Task InsertPipelineAsync(
        IDbConnection conn, IDbTransaction tx, int configId, IReadOnlyList<StepConfig> steps, CancellationToken cancellationToken)
    {
        const string stepSql = """
            INSERT INTO "PipelineStep" (
                "CoverageConfigId", "StepOrder", "StepId", "Name", "Operation",
                "RateTableName", "MathType", "InterpolateKey", "RangeKeyName",
                "ComputeExpr", "ComputeStoreAs", "ComputeApplyToPremium",
                "RoundPrecision", "RoundMode",
                "WhenPath", "WhenOperator", "WhenValue")
            VALUES (
                @CoverageConfigId, @StepOrder, @StepId, @Name, @Operation,
                @RateTableName, @MathType, @InterpolateKey, @RangeKeyName,
                @ComputeExpr, @ComputeStoreAs, @ComputeApplyToPremium,
                @RoundPrecision, @RoundMode,
                @WhenPath, @WhenOperator, @WhenValue)
            RETURNING "Id"
            """;
        const string keySql = """
            INSERT INTO "PipelineStepKey" ("PipelineStepId", "KeyName", "KeyValue")
            VALUES (@PipelineStepId, @KeyName, @KeyValue)
            """;
        const string clauseSql = """
            INSERT INTO "PipelineStepWhenClause" ("PipelineStepId", "GroupId", "ClausePath", "ClauseOp", "ClauseValue", "SortOrder")
            VALUES (@PipelineStepId, @GroupId, @ClausePath, @ClauseOp, @ClauseValue, @SortOrder)
            """;

        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            var groups = SqlCoverageAdminRepository.ExtractWhenGroups(s.When);
            var (whenPath, whenOp, whenVal) = groups.Count == 0 ? SqlCoverageAdminRepository.ExtractWhen(s.When) : (null, null, null);

            var stepId = await conn.QuerySingleAsync<int>(new CommandDefinition(stepSql, new
            {
                CoverageConfigId      = configId,
                StepOrder             = i,
                StepId                = s.Id,
                s.Name, s.Operation,
                RateTableName         = s.RateTable,
                MathType              = s.Math?.Type,
                InterpolateKey        = s.Interpolate?.Key,
                RangeKeyName          = s.RangeKey?.Key,
                ComputeExpr           = s.Compute?.Expr,
                ComputeStoreAs        = s.Compute?.StoreAs,
                ComputeApplyToPremium = s.Compute?.ApplyToPremium,
                RoundPrecision        = s.Round?.Precision,
                RoundMode             = s.Round?.Mode,
                WhenPath = whenPath, WhenOperator = whenOp, WhenValue = whenVal
            }, transaction: tx, cancellationToken: cancellationToken));

            if (s.Keys is not null)
                foreach (var kv in s.Keys)
                    await conn.ExecuteAsync(new CommandDefinition(keySql,
                        new { PipelineStepId = stepId, KeyName = kv.Key, KeyValue = kv.Value },
                        transaction: tx, cancellationToken: cancellationToken));

            foreach (var (groupId, clauses) in groups)
                for (int ci = 0; ci < clauses.Count; ci++)
                {
                    var (cp, co, cv) = clauses[ci];
                    await conn.ExecuteAsync(new CommandDefinition(clauseSql,
                        new { PipelineStepId = stepId, GroupId = groupId, ClausePath = cp, ClauseOp = co, ClauseValue = cv, SortOrder = ci },
                        transaction: tx, cancellationToken: cancellationToken));
                }
        }
    }

    private static async Task InsertPerilsAsync(IDbConnection conn, IDbTransaction tx, int configId, IReadOnlyList<string> perils, CancellationToken cancellationToken)
    {
        const string sql = """INSERT INTO "CoveragePeril" ("CoverageConfigId", "PerilCode", "SortOrder") VALUES (@CoverageConfigId, @PerilCode, @SortOrder) """;
        for (int i = 0; i < perils.Count; i++)
            await conn.ExecuteAsync(new CommandDefinition(sql, new { CoverageConfigId = configId, PerilCode = perils[i], SortOrder = i }, transaction: tx, cancellationToken: cancellationToken));
    }

    private static async Task InsertDependenciesAsync(IDbConnection conn, IDbTransaction tx, int configId, IReadOnlyList<string> dependsOn, CancellationToken cancellationToken)
    {
        const string sql = """INSERT INTO "CoverageDependency" ("CoverageConfigId", "DependsOnCode", "SortOrder") VALUES (@CoverageConfigId, @DependsOnCode, @SortOrder) """;
        for (int i = 0; i < dependsOn.Count; i++)
            await conn.ExecuteAsync(new CommandDefinition(sql, new { CoverageConfigId = configId, DependsOnCode = dependsOn[i], SortOrder = i }, transaction: tx, cancellationToken: cancellationToken));
    }

    private static async Task InsertPublishKeysAsync(IDbConnection conn, IDbTransaction tx, int configId, IReadOnlyList<string> publish, CancellationToken cancellationToken)
    {
        const string sql = """INSERT INTO "CoveragePublish" ("CoverageConfigId", "PublishKey", "SortOrder") VALUES (@CoverageConfigId, @PublishKey, @SortOrder) """;
        for (int i = 0; i < publish.Count; i++)
            await conn.ExecuteAsync(new CommandDefinition(sql, new { CoverageConfigId = configId, PublishKey = publish[i], SortOrder = i }, transaction: tx, cancellationToken: cancellationToken));
    }

    private static async Task InsertAggregateConfigAsync(IDbConnection conn, IDbTransaction tx, int configId, AggregateConfigRequest req, CancellationToken cancellationToken)
    {
        const string aggSql = """
            INSERT INTO "CoverageAggregateConfig" ("CoverageConfigId", "WhenPath", "WhenOp", "WhenValue")
            VALUES (@CoverageConfigId, @WhenPath, @WhenOp, @WhenValue)
            RETURNING "Id"
            """;
        const string fieldSql = """
            INSERT INTO "CoverageAggregateField" ("CoverageAggregateConfigId", "SourceField", "AggFunction", "ResultKey", "SortOrder")
            VALUES (@CoverageAggregateConfigId, @SourceField, @AggFunction, @ResultKey, @SortOrder)
            """;

        var aggId = await conn.QuerySingleAsync<int>(new CommandDefinition(
            aggSql, new { CoverageConfigId = configId, req.WhenPath, req.WhenOp, req.WhenValue },
            transaction: tx, cancellationToken: cancellationToken));

        for (int i = 0; i < req.Fields.Count; i++)
        {
            var f = req.Fields[i];
            await conn.ExecuteAsync(new CommandDefinition(fieldSql,
                new { CoverageAggregateConfigId = aggId, f.SourceField, f.AggFunction, f.ResultKey, SortOrder = i },
                transaction: tx, cancellationToken: cancellationToken));
        }
    }

    private static StepConfig BuildStep(StepRow s,
        Dictionary<int, Dictionary<string, string>> keysByStep,
        Dictionary<int, List<SqlCoverageAdminRepository.WhenClauseRow>> whenByStep)
    {
        keysByStep.TryGetValue(s.Id, out var keys);
        whenByStep.TryGetValue(s.Id, out var clauseRows);

        WhenConfig? when;
        if (clauseRows is { Count: > 0 })
            when = SqlCoverageAdminRepository.BuildWhenFromClauses(clauseRows);
        else
            when = !string.IsNullOrEmpty(s.WhenPath) && !string.IsNullOrEmpty(s.WhenOperator)
                   ? SqlCoverageAdminRepository.BuildWhen(s.WhenPath, s.WhenOperator, s.WhenValue)
                   : null;

        return new StepConfig
        {
            Id        = s.StepId, Name = s.Name, Operation = s.Operation, RateTable = s.RateTableName,
            Keys      = keys,
            Math      = s.MathType is not null ? new MathConfig { Type = s.MathType } : null,
            Compute   = s.ComputeExpr is not null && s.ComputeStoreAs is not null
                        ? new ComputeConfig { Expr = s.ComputeExpr, StoreAs = s.ComputeStoreAs, ApplyToPremium = s.ComputeApplyToPremium ?? false } : null,
            Round     = s.RoundPrecision.HasValue ? new RoundConfig { Precision = s.RoundPrecision.Value, Mode = s.RoundMode ?? "AwayFromZero" } : null,
            Interpolate = s.InterpolateKey is not null ? new InterpolateConfig { Key = s.InterpolateKey } : null,
            RangeKey    = s.RangeKeyName   is not null ? new RangeKeyConfig    { Key = s.RangeKeyName   } : null,
            When        = when,
        };
    }

    // ── Row types ──────────────────────────────────────────────────────────────

    private sealed class CoverageRow
    {
        public int Id { get; init; }
        public int CoverageRefId { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public string State { get; init; } = "*";
        public string CoverageCode { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public DateOnly EffStart { get; init; }
        public DateOnly? ExpireAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public string? CreatedBy { get; init; }
        public DateTime? ModifiedAt { get; init; }
        public string? ModifiedBy { get; init; }
        public string? Notes { get; init; }
    }

    private sealed class StepRow
    {
        public int Id { get; init; }
        public int StepOrder { get; init; }
        public string StepId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Operation { get; init; } = string.Empty;
        public string? RateTableName { get; init; }
        public string? MathType { get; init; }
        public string? InterpolateKey { get; init; }
        public string? RangeKeyName { get; init; }
        public string? ComputeExpr { get; init; }
        public string? ComputeStoreAs { get; init; }
        public bool? ComputeApplyToPremium { get; init; }
        public int? RoundPrecision { get; init; }
        public string? RoundMode { get; init; }
        public string? WhenPath { get; init; }
        public string? WhenOperator { get; init; }
        public string? WhenValue { get; init; }
    }

    private sealed class StepKeyRow
    {
        public int PipelineStepId { get; init; }
        public string KeyName { get; init; } = string.Empty;
        public string KeyValue { get; init; } = string.Empty;
    }

    private sealed class AggRow
    {
        public int Id { get; init; }
        public string WhenPath { get; init; } = string.Empty;
        public string WhenOp { get; init; } = string.Empty;
        public string WhenValue { get; init; } = string.Empty;
        public int? FieldId { get; init; }
        public string? SourceField { get; init; }
        public string? AggFunction { get; init; }
        public string? ResultKey { get; init; }
        public int FieldSort { get; init; }
    }
}
