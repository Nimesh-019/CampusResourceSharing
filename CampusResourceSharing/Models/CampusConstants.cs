namespace CampusResourceSharing.Models
{
    public static class DepartmentConstants
    {
        public static readonly List<string> Departments = new()
        {
            "Computer Science / IT",
            "Mechanical Engineering",
            "Civil Engineering",
            "Electrical Engineering",
            "Electronics",
            "Other"
        };
    }

    public static class ItemConstants
    {
        public static readonly List<string> Categories = new()
        {
            "Books",
            "Electronics",
            "Calculators",
            "Lab Equipment",
            "Stationery",
            "Tools",
            "Sports",
            "Other"
        };

        public static readonly List<string> Conditions = new()
        {
            "New",
            "Like New",
            "Good",
            "Fair"
        };
    }
}
