using System;

namespace Shared.Kernel.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message)
        {
        }
    }

    public class BadRequestException : Exception
    {
        public BadRequestException(string message) : base(message)
        {
        }
    }

    public class UnauthorizedException : Exception
    {
        public UnauthorizedException(string message) : base(message)
        {
        }
    }

    public class InvalidDomainException : Exception
    {
        public InvalidDomainException(string message) : base(message)
        {
        }
    }

    public class ClubNotFoundException : Exception
    {
        public ClubNotFoundException(Guid clubId) 
            : base($"Club with ID {clubId} was not found.")
        {
        }
    }
}
