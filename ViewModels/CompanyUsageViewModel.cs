namespace CloudMVCApplication.ViewModels
{
    public sealed record CompanyUsageViewModel(
        string Plan,
        int PropertyCount,
        int MaxProperties,
        int UnitCount,
        int MaxUnits,
        int ManagerCount,
        int MaxManagers)
    {
        public int RemainingUnits => Math.Max(0, MaxUnits - UnitCount);

        public int RemainingProperties => Math.Max(0, MaxProperties - PropertyCount);

        public bool HasUnlimitedManagers => MaxManagers == int.MaxValue;
    }
}
