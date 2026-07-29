namespace ESTAFF.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class removeProfilePicturePath : DbMigration
    {
        public override void Up()
        {
            DropColumn("CLIP.AspNetUsers", "ProfilePicturePath");

        }
        
        public override void Down()
        {
            AddColumn("CLIP.AspNetUsers", "ProfilePicturePath", c => c.String());
        }
    }
}
