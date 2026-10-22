namespace Dsw2025Tpi.Domain.Entities
{
    public abstract class EntityBase
    {
        protected EntityBase()
        {
            if (Id == Guid.Empty)
                Id = Guid.NewGuid();
        }
        public Guid Id { get; set; }
    }
}
   

