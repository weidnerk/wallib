using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wallib.Models
{
    public class walDB : DbContext
    {
        static walDB()
        {
            //do not try to create a database 
            Database.SetInitializer<walDB>(null);
        }

        public walDB()
            : base("name=OPWContext")
        {
        }

        public DbSet<WalItem> WalItems { get; set; }
        public DbSet<WalMap> WalMapItems { get; set; }

        public async Task ItemStore(WalItem item)
        {
            try
            {
                WalItems.Add(item);
                await this.SaveChangesAsync();
            }
            catch
            {
                throw;
            }
        }

        public void SellerResultSave(WalMap record)
        {
            this.WalMapItems.Add(record);
            this.SaveChanges();
        }

        public void RemoveItemRecords(int categoryId)
        {
            Database.ExecuteSqlCommand("delete from WalItems where categoryId=" + categoryId.ToString());
        }
        public void RemoveMapRecords(int categoryId)
        {
            Database.ExecuteSqlCommand("delete from WalItems where categoryId=" + categoryId.ToString());
        }
    }
}
