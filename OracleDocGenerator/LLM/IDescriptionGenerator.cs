public interface IDescriptionGenerator
{
    Task GenerateMissingColumnDescriptionsAsync(TableInfo table);
}