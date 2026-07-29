namespace ESTAFF.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class changeNameCOFIdToSubtaskId : DbMigration
    {
        public override void Up()
        {
            AddColumn("ESTAFF.TaskItems", "SubTaskId", c => c.Int());
            DropColumn("ESTAFF.TaskItems", "COFId");
        }
        
        public override void Down()
        {
            AddColumn("ESTAFF.TaskItems", "COFId", c => c.Int());
            DropColumn("ESTAFF.TaskItems", "SubTaskId");
        }
    }
}
