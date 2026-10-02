namespace CampusResourceSharing.Models
{
    public static class DepartmentConstants
    {
        public static readonly List<string> Departments = new()
        {
            "Computer Science",
            "Mechanical Engineering",
            "Civil Engineering",
            "Electrical Engineering",
            "Information Technology"
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
            "Sports"
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
