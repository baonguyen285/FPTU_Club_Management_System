using AutoMapper;
using Notification.Application.DTOs;
using Notification.Domain.Entities;

namespace Notification.Application.Mappings
{
    public class NotificationMappingProfile : Profile
    {
        public NotificationMappingProfile()
        {
            CreateMap<NotificationEntity, NotificationDto>();
        }
    }
}
