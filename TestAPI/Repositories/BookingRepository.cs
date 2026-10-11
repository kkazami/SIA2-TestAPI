using TestAPI.Models;

namespace TestAPI.Repositories;

public class BookingRepository(BookingDbContext db)
{
    public IQueryable<UserAccount> Users => db.Users;
    public IQueryable<Availability> Slots => db.Slots;
    public IQueryable<Appointment> Appointments => db.Appointments;
    public IQueryable<LoginSession> Sessions => db.Sessions;

    public void Add<T>(T record) where T : class => db.Add(record);
    public void Remove<T>(T record) where T : class => db.Remove(record);

    // A transaction keeps the check and save together. SQLite serializes writes.
    public T Write<T>(Func<T> action)
    {
        using var transaction = db.Database.BeginTransaction();
        var result = action();
        db.SaveChanges();
        transaction.Commit();
        return result;
    }
}
