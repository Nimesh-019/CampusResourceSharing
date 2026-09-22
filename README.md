CAMPUS RESOURCE SHARING
=======================

Campus Resource Sharing is an ASP.NET Core MVC web application that allows students to share and request resources within a campus community.


FRESH SETUP
===========

Follow the steps below to run the application after cloning the repository.


1. CLONE THE REPOSITORY
-----------------------

Open a terminal and run:

git clone https://github.com/nimesh-019/CampusResourceSharing.git


2. OPEN THE PROJECT DIRECTORY
-----------------------------

Navigate to the cloned project:

cd CampusResourceSharing

The solution file is located in this directory:

CampusResourceSharing.slnx


3. CONFIGURE THE DATABASE
-------------------------

Open:

CampusResourceSharing/appsettings.json

Find the following section:

"ConnectionStrings": {
    "DefaultConnection": "..."
}

Update the connection string according to your local SQL Server configuration.


Example: SQL Server

"ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CampusResourceSharingDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}


Example: SQL Server Express

If you are using SQL Server Express:

"ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=CampusResourceSharingDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}

Use the SQL Server instance available on your computer.


4. RESTORE DEPENDENCIES
-----------------------

Run:

dotnet restore


5. CREATE AND UPDATE THE DATABASE
---------------------------------

The Entity Framework Core migration files are already included in the repository.

Run:

dotnet ef database update

This will:

- Create the database if it does not already exist.
- Create all required tables.
- Apply the existing migrations.
- Create the ASP.NET Core Identity tables.

DO NOT run Add-Migration during the initial setup.


6. RUN THE APPLICATION
----------------------

Run:

dotnet run

The terminal will display the URL where the application is running.

For example:

http://localhost:5256

Open the displayed URL in your browser.


DATABASE
========

The actual SQL Server database is NOT included in the repository.

Only the Entity Framework Core migration files are included.

Each developer creates their own local database by running:

dotnet ef database update

Therefore, every developer starts with a fresh local database.


UPLOADED IMAGES
===============

Uploaded item images are stored locally in:

CampusResourceSharing/wwwroot/uploads/

Uploaded images are NOT included in the GitHub repository.

Each developer has their own local image storage.


QUICK SETUP
===========

If all required software is already installed:

git clone https://github.com/nimesh-019/CampusResourceSharing.git

cd CampusResourceSharing

Configure the SQL Server connection string in:

CampusResourceSharing/appsettings.json

Then run:

dotnet restore

dotnet ef database update

dotnet run

Open the URL displayed in the terminal.


IMPORTANT NOTES
===============

- The SQL Server database is not included in the repository.

- Uploaded item images are not included in the repository.

- Entity Framework Core migration files are included in the repository.

- Each developer creates their own local database.

- Each developer has their own local uploaded images.

- Do not run Add-Migration during the initial setup.

- If your SQL Server instance name is different, update DefaultConnection in appsettings.json.
