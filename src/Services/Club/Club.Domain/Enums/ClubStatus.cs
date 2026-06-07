using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Club.Domain.Enums
{
    public enum ClubStatus
    {
        PendingApproval = 0, // CLB mới nộp đơn đăng ký thành lập, chờ trường duyệt
        Active = 1,          // CLB đang hoạt động bình thường
        Suspended = 2,       // CLB đang bị tạm dừng/đình chỉ hoạt động (do vi phạm)
        Inactive = 3         // CLB đã giải thể
    }
}
