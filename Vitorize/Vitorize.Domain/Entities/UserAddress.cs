using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class UserAddress
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = null!;

    public string ReceiverName { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string? Province { get; set; }

    public string? City { get; set; }

    public string AddressLine { get; set; } = null!;

    public string? PostalCode { get; set; }

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
