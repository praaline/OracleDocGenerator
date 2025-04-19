public class MockDescriptionGenerator : IDescriptionGenerator
{
    public Task GenerateMissingColumnDescriptionsAsync(TableInfo table)
    {
        foreach (var column in table.Columns)
        {
            if (string.IsNullOrWhiteSpace(column.Comment))
            {
                column.Comment = $"Auto-generated description for {column.Name}";
            }
        }

        return Task.CompletedTask;
    }
}
