using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Club.Domain.Enums
{
    public enum MembershipStatus
    {
        Pending = 0,  // Đang nộp đơn chờ Chủ nhiệm CLB duyệt vào nhóm
        Approved = 1, // Đã chính thức là thành viên
        Rejected = 2, // Bị từ chối cho gia nhập
        Left = 3      // Đã chủ động rời khỏi CLB
    }
}
