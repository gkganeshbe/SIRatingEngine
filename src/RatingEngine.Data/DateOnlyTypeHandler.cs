using System.Data;
using Dapper;

namespace RatingEngine.Data;

public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public static readonly DateOnlyTypeHandler Instance = new();
    private DateOnlyTypeHandler() { }

    public override DateOnly Parse(object value) =>
        DateOnly.FromDateTime((DateTime)value);

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }
}
