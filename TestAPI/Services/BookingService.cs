using TestAPI.Models;
using TestAPI.Repositories;

namespace TestAPI.Services;

public class BookingService(BookingRepository repository)
{
    private UserAccount User(int id) => repository.Users.SingleOrDefault(u => u.Id == id)
        ?? throw new ApiException(404, "User not found.");
    private Availability Slot(int id) => repository.Slots.SingleOrDefault(s => s.Id == id)
        ?? throw new ApiException(404, "Slot not found.");
    private Appointment Find(int id) => repository.Appointments.SingleOrDefault(a => a.Id == id)
        ?? throw new ApiException(404, "Appointment not found.");

    private static void CheckOwner(int actorId, string role, int ownerId)
    {
        if (role != "Admin" && actorId != ownerId)
            throw new ApiException(403, "You can only manage your own records.");
    }

    private static bool Overlaps(Availability first, Availability second) =>
        first.StartTime < second.EndTime && second.StartTime < first.EndTime;

    private void ValidateSlot(Availability slot)
    {
        var tutor = User(slot.TutorId);
        if (!tutor.IsActive || tutor.Role != "Tutor")
            throw new ApiException(400, "TutorId must identify an active tutor.");
        if (slot.StartTime <= DateTime.UtcNow || slot.EndTime <= slot.StartTime)
            throw new ApiException(400, "Start must be in the future; end must follow start.");
        if (repository.Slots.Where(s => s.TutorId == slot.TutorId && s.Id != slot.Id)
            .ToList().Any(s => Overlaps(s, slot)))
            throw new ApiException(409, "Tutor slots cannot overlap.");
    }

    public List<Availability> Available() => repository.Slots.Where(s =>
        s.StartTime > DateTime.UtcNow
        && repository.Users.Any(u => u.Id == s.TutorId && u.IsActive)
        && !repository.Appointments.Any(a =>
            a.AvailabilityId == s.Id && a.Status != "Cancelled"))
        .OrderBy(s => s.StartTime).ToList();

    public Availability CreateSlot(int actorId, string role, AvailabilityRequest request) =>
        repository.Write(() =>
        {
            CheckOwner(actorId, role, request.TutorId);
            var slot = new Availability
            {
                TutorId = request.TutorId,
                StartTime = request.StartTime.UtcDateTime,
                EndTime = request.EndTime.UtcDateTime
            };
            ValidateSlot(slot);
            repository.Add(slot);
            return slot;
        });

    public Availability UpdateSlot(int actorId, string role, int id,
        AvailabilityRequest request) => repository.Write(() =>
    {
        var slot = Slot(id);
        CheckOwner(actorId, role, slot.TutorId);
        CheckOwner(actorId, role, request.TutorId);
        if (repository.Appointments.Any(a => a.AvailabilityId == id))
            throw new ApiException(409, "Slot has appointment history. Create a new slot instead.");
        slot.TutorId = request.TutorId;
        slot.StartTime = request.StartTime.UtcDateTime;
        slot.EndTime = request.EndTime.UtcDateTime;
        ValidateSlot(slot);
        return slot;
    });

    public void DeleteSlot(int actorId, string role, int id) => repository.Write(() =>
    {
        var slot = Slot(id);
        CheckOwner(actorId, role, slot.TutorId);
        if (repository.Appointments.Any(a => a.AvailabilityId == id))
            throw new ApiException(409, "Cannot delete a slot with appointment history.");
        repository.Remove(slot);
        return true;
    });

    private void CheckAppointmentOwner(int actorId, string role, Appointment appointment)
    {
        var owner = role == "Tutor" ? Slot(appointment.AvailabilityId).TutorId
            : appointment.StudentId;
        CheckOwner(actorId, role, owner);
    }

    // GET includes names and times for easier reading. Booking uses IDs.
    private object View(Appointment appointment)
    {
        var slot = Slot(appointment.AvailabilityId);
        return new
        {
            appointment.Id, appointment.StudentId, appointment.AvailabilityId,
            appointment.Status, tutorId = slot.TutorId,
            studentName = User(appointment.StudentId).Name,
            tutorName = User(slot.TutorId).Name,
            startTime = slot.StartTime, endTime = slot.EndTime
        };
    }

    public List<object> GetAll(int actorId, string role)
    {
        var query = repository.Appointments;
        if (role == "Student") query = query.Where(a => a.StudentId == actorId);
        if (role == "Tutor") query = query.Where(a =>
            repository.Slots.Any(s => s.Id == a.AvailabilityId && s.TutorId == actorId));
        return query.OrderBy(a => a.Id).ToList().Select(View).ToList();
    }

    public object Get(int actorId, string role, int id)
    {
        var appointment = Find(id);
        CheckAppointmentOwner(actorId, role, appointment);
        return View(appointment);
    }

    private void CheckBooking(int studentId, Availability slot, int excludingId = 0)
    {
        if (slot.StartTime <= DateTime.UtcNow || !User(slot.TutorId).IsActive)
            throw new ApiException(400, "Slot must be in the future with an active tutor.");
        var bookings = repository.Appointments.Where(a =>
            a.Status != "Cancelled" && a.Id != excludingId).ToList();
        if (bookings.Any(a => a.AvailabilityId == slot.Id))
            throw new ApiException(409, "Slot is already booked.");
        if (bookings.Where(a => a.StudentId == studentId)
            .Any(a => Overlaps(Slot(a.AvailabilityId), slot)))
            throw new ApiException(409, "Student already has an appointment at this time.");
    }

    public Appointment Book(int studentId, BookAppointmentRequest request) =>
        repository.Write(() =>
        {
            var slot = Slot(request.AvailabilityId);
            CheckBooking(studentId, slot);
            var appointment = new Appointment
            {
                StudentId = studentId, AvailabilityId = slot.Id
            };
            repository.Add(appointment);
            return appointment;
        });

    public Appointment Update(int actorId, string role, int id,
        UpdateAppointmentRequest request) => repository.Write(() =>
    {
        var appointment = Find(id);
        CheckAppointmentOwner(actorId, role, appointment);
        if (request.AvailabilityId == null && request.Status == null)
            throw new ApiException(400, "Provide availabilityId or status.");
        if (appointment.Status == "Cancelled")
            throw new ApiException(409, "Cancelled appointments cannot be reopened.");
        if (Slot(appointment.AvailabilityId).StartTime <= DateTime.UtcNow)
            throw new ApiException(409, "Past appointments cannot be changed.");
        if (request.Status != null && request.Status is not ("Cancelled" or "Confirmed"))
            throw new ApiException(400, "Status must be Cancelled or Confirmed.");
        if (request.Status == "Confirmed" && role == "Student")
            throw new ApiException(403, "Only tutors and admins can confirm.");
        if (request.AvailabilityId != null)
        {
            if (role == "Tutor") throw new ApiException(403, "Tutors cannot reschedule.");
            if (request.Status == "Cancelled")
                throw new ApiException(400, "Cancel and reschedule are separate actions.");
            if (!User(appointment.StudentId).IsActive)
                throw new ApiException(409, "Student is inactive.");
            var slot = Slot(request.AvailabilityId.Value);
            CheckBooking(appointment.StudentId, slot, id);
            appointment.AvailabilityId = slot.Id;
            appointment.Status = "Booked";
        }
        if (request.Status != null) appointment.Status = request.Status;
        return appointment;
    });
}
