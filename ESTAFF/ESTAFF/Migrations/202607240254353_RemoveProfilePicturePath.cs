namespace ESTAFF.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveProfilePicturePath : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.AspNetUsers", "ProfilePicturePath");
        }
        
        public override void Down()
        {
            AddColumn("dbo.AspNetUsers", "ProfilePicturePath", c => c.String());
        }
    }
}
