// Payment handling JavaScript
class PaymentHandler {
    constructor() {
        this.initializeEventListeners();
    }

    initializeEventListeners() {
        // Xử lý nút thanh toán khóa học
        $(document).on('click', '.btn-payos-payment', (e) => {
            e.preventDefault();
            this.processPayOSPayment();
        });

        // Xử lý nút thanh toán Subscription
        $(document).on('click', '.btn-subscription-payos', (e) => {
            e.preventDefault();
            const planId = $(e.target).data('plan-id');
            this.processSubscriptionPayment(planId);
        });

        // Kiểm tra quyền truy cập khóa học
        $(document).on('click', '.check-course-access', (e) => {
            e.preventDefault();
            const courseId = $(e.target).data('course-id');
            this.checkCourseAccess(courseId);
        });
    }

    async processPayOSPayment() {
        try {
            this.showLoading('Đang tạo thanh toán...');

            const response = await fetch('/Payment/CreatePayOSPayment', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                }
            });

            const result = await response.json();

            if (!result || !result.payUrl) {
                this.showError(result.message || 'Không tạo được liên kết thanh toán PayOS');
                return;
            }

            const qrImageUrl = `https://img.vietqr.io/image/${result.bin}-${result.accountNumber}-compact2.jpg?amount=${result.amount}&addInfo=${encodeURIComponent(result.description)}&accountName=${encodeURIComponent(result.accountName)}`;
            this.showQRCode(qrImageUrl, result.orderId, result.amount, result.description, result.payUrl);
        } catch (error) {
            console.error('Payment error:', error);
            this.showError('Có lỗi xảy ra khi xử lý thanh toán');
        } finally {
            this.hideLoading();
        }
    }

    async processSubscriptionPayment(planId) {
        try {
            this.showLoading('Đang tạo thanh toán...');

            const response = await fetch('/Payment/CreateSubscriptionPayOSPayment', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                },
                body: JSON.stringify({ planId: parseInt(planId) })
            });

            const result = await response.json();

            if (!result || !result.payUrl) {
                this.showError(result.message || 'Không tạo được liên kết thanh toán PayOS');
                return;
            }

            const qrImageUrl = `https://img.vietqr.io/image/${result.bin}-${result.accountNumber}-compact2.jpg?amount=${result.amount}&addInfo=${encodeURIComponent(result.description)}&accountName=${encodeURIComponent(result.accountName)}`;
            this.showQRCode(qrImageUrl, result.orderId, result.amount, result.description, result.payUrl);
        } catch (error) {
            console.error('Payment error:', error);
            this.showError('Có lỗi xảy ra khi xử lý thanh toán');
        } finally {
            this.hideLoading();
        }
    }

    async checkCourseAccess(courseId) {
        try {
            const response = await fetch(`/Payment/CheckCourseAccess?courseId=${courseId}`);
            const result = await response.json();

            if (result.hasAccess) {
                this.showSuccess('Bạn đã có quyền truy cập khóa học này');
                setTimeout(() => {
                    window.location.href = `/Course/Detail/${courseId}`;
                }, 1500);
            } else {
                this.showInfo('Bạn chưa mua khóa học này');
            }
        } catch (error) {
            console.error('Access check error:', error);
            this.showError('Có lỗi xảy ra khi kiểm tra quyền truy cập');
        }
    }

    showQRCode(qrCodeUrl, orderId, amount, description, payUrl) {
        // Đảm bảo xóa modal cũ nếu có
        $('#qrCodeModal').remove();

        const modal = `
            <div class="modal fade" id="qrCodeModal" tabindex="-1">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content">
                        <div class="modal-header bg-primary text-white">
                            <h5 class="modal-title"><i class="fas fa-qrcode me-2"></i>Thanh toán qua VietQR</h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body text-center">
                            <div class="mb-3">
                                <img src="${qrCodeUrl}" alt="QR Code" class="img-fluid border rounded p-2 shadow-sm" style="max-width: 260px;">
                            </div>
                            <div class="text-start px-4 mb-3 bg-light p-2 rounded">
                                <div class="mb-1"><strong>Mã đơn hàng:</strong> <span class="text-dark">${orderId}</span></div>
                                <div class="mb-1"><strong>Số tiền:</strong> <span class="text-primary fw-bold">${Number(amount).toLocaleString('vi-VN')} ₫</span></div>
                                <div class="mb-1"><strong>Nội dung:</strong> <span class="text-danger fw-bold">${description}</span></div>
                            </div>
                            <div class="alert alert-info py-2 px-3 small text-start mb-3">
                                <i class="fas fa-info-circle me-1"></i> Sử dụng App Ngân hàng quét mã QR trên để thanh toán nhanh, hoặc mở link cổng PayOS chính thức.
                            </div>
                            <div class="d-grid gap-2">
                                <button type="button" class="btn btn-success btn-lg id-btn-simulate-success">
                                    <i class="fas fa-check-circle me-2"></i>Giả lập thanh toán thành công
                                </button>
                                <a href="${payUrl}" target="_blank" class="btn btn-outline-primary btn-sm">
                                    <i class="fas fa-external-link-alt me-2"></i>Mở trang thanh toán PayOS thật
                                </a>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('body').append(modal);
        $('#qrCodeModal').modal('show');

        // Bắt sự kiện click giả lập thanh toán
        $('#qrCodeModal').off('click', '.id-btn-simulate-success').on('click', '.id-btn-simulate-success', async () => {
            try {
                this.showLoading('Đang giả lập thanh toán...');
                const response = await fetch('/Payment/SimulateSuccess', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                    },
                    body: JSON.stringify({ orderId: parseInt(orderId) })
                });

                const result = await response.json();
                if (result.success) {
                    $('#qrCodeModal').modal('hide');
                    this.showSuccess('Giả lập thanh toán thành công!');
                    setTimeout(() => {
                        window.location.href = `/Payment/PayOSReturn?orderCode=${orderId}&status=PAID`;
                    }, 1000);
                } else {
                    this.showError(result.message || 'Lỗi khi giả lập thanh toán');
                }
            } catch (error) {
                console.error(error);
                this.showError('Có lỗi xảy ra');
            } finally {
                this.hideLoading();
            }
        });

        // Xóa modal khi đóng
        $('#qrCodeModal').on('hidden.bs.modal', function () {
            $(this).remove();
        });
    }

    showLoading(message = 'Đang xử lý...') {
        const loadingHtml = `
            <div id="payment-loading" class="position-fixed top-0 start-0 w-100 h-100 d-flex align-items-center justify-content-center" 
                 style="background: rgba(0,0,0,0.5); z-index: 9999;">
                <div class="bg-white p-4 rounded text-center">
                    <div class="spinner-border text-primary mb-3" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <div>${message}</div>
                </div>
            </div>
        `;
        $('body').append(loadingHtml);
    }

    hideLoading() {
        $('#payment-loading').remove();
    }

    showSuccess(message) {
        this.showToast(message, 'success');
    }

    showError(message) {
        this.showToast(message, 'error');
    }

    showInfo(message) {
        this.showToast(message, 'info');
    }

    showToast(message, type = 'info') {
        const bgClass = {
            'success': 'bg-success',
            'error': 'bg-danger',
            'info': 'bg-info'
        }[type] || 'bg-info';

        const toastHtml = `
            <div class="toast align-items-center text-white ${bgClass} border-0" role="alert" style="position: fixed; top: 20px; right: 20px; z-index: 9999;">
                <div class="d-flex">
                    <div class="toast-body">
                        ${message}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
                </div>
            </div>
        `;

        $('body').append(toastHtml);
        const toast = new bootstrap.Toast($('.toast').last()[0]);
        toast.show();

        $('.toast').last().on('hidden.bs.toast', function () {
            $(this).remove();
        });
    }
}

// Khởi tạo PaymentHandler khi document ready
$(document).ready(() => {
    new PaymentHandler();
});