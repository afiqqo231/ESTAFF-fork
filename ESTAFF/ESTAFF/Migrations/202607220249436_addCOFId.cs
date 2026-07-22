namespace ESTAFF.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class addCOFId : DbMigration
    {
        public override void Up()
        {
            AddColumn("ESTAFF.TaskItems", "COFId", c => c.Int());
        }
        
        public override void Down()
        {
            DropColumn("ESTAFF.TaskItems", "COFId");
        }
    }
}
