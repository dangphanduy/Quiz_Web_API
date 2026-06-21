// Payment handling JavaScript
class PaymentHandler {
    constructor() {
        this.currencyFormatter = new Intl.NumberFormat('vi-VN');
        this.initializeSelectionState();
        this.initializeEventListeners();
    }

    initializeSelectionState() {
        this.updateCheckoutSummary();
    }

    initializeEventListeners() {
        $(document).on('change', '.course-select-input', () => {
            this.updateCheckoutSummary();
        });

        $(document).on('change', '#selectAllCourses', (e) => {
            $('.course-select-input').prop('checked', e.currentTarget.checked);
            this.updateCheckoutSummary();
        });

        $(document).on('click', '[data-course-card]', (e) => {
            if ($(e.target).is('input, button, a, i') || $(e.target).closest('button, a').length) {
                return;
            }

            const checkbox = $(e.currentTarget).find('.course-select-input');
            checkbox.prop('checked', !checkbox.prop('checked')).trigger('change');
        });

        $(document).on('click', '.btn-momo-payment', (e) => {
            e.preventDefault();
            this.processMoMoPayment();
        });

        $(document).on('click', '.check-course-access', (e) => {
            e.preventDefault();
            const courseId = $(e.target).data('course-id');
            this.checkCourseAccess(courseId);
        });
    }

    getSelectedCourseIds() {
        return $('.course-select-input:checked')
            .map((_, element) => Number(element.value))
            .get()
            .filter(courseId => Number.isInteger(courseId) && courseId > 0);
    }

    getSelectionSnapshot() {
        const selectedInputs = $('.course-select-input:checked');
        const selectedCount = selectedInputs.length;
        const totalCount = $('.course-select-input').length;
        let total = 0;

        selectedInputs.each((_, element) => {
            total += Number(element.dataset.price || 0);
        });

        return { selectedCount, totalCount, total };
    }

    updateCheckoutSummary() {
        const snapshot = this.getSelectionSnapshot();
        const formattedTotal = `${this.currencyFormatter.format(snapshot.total)} VNĐ`;
        const hasSelection = snapshot.selectedCount > 0;

        $('[data-course-card]').each((_, card) => {
            const isSelected = $(card).find('.course-select-input').prop('checked');
            $(card).toggleClass('selected', isSelected);
        });

        $('#selectedCount, #summarySelectedCount').text(snapshot.selectedCount);
        $('#summarySubtotal, #summaryTotal').text(formattedTotal);
        $('#selectionWarning').toggleClass('d-none', hasSelection);
        $('.btn-momo-payment').prop('disabled', !hasSelection);

        const selectAll = $('#selectAllCourses');
        if (selectAll.length) {
            const selectAllElement = selectAll.get(0);
            selectAll.prop('checked', snapshot.totalCount > 0 && snapshot.selectedCount === snapshot.totalCount);
            selectAllElement.indeterminate = snapshot.selectedCount > 0 && snapshot.selectedCount < snapshot.totalCount;
        }
    }

    async processMoMoPayment() {
        const selectedCourseIds = this.getSelectedCourseIds();
        if (!selectedCourseIds.length) {
            this.showError('Vui lòng chọn ít nhất một khóa học để thanh toán');
            this.updateCheckoutSummary();
            return;
        }

        try {
            this.showLoading('Đang tạo thanh toán...');

            const response = await fetch('/Payment/CreateMoMoPayment', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                },
                body: JSON.stringify({ courseIds: selectedCourseIds })
            });

            const result = await response.json();

            if (!result || !result.payUrl) {
                this.showError(result.message || 'Không tạo được liên kết thanh toán MoMo');
                return;
            }

            window.location.href = result.payUrl;
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

    showLoading(message = 'Đang xử lý...') {
        const loadingHtml = `
            <div id="payment-loading" class="position-fixed top-0 start-0 w-100 h-100 d-flex align-items-center justify-content-center"
                 style="background: rgba(0,0,0,0.5); z-index: 9999;">
                <div class="bg-white p-4 rounded text-center shadow">
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
            success: 'bg-success',
            error: 'bg-danger',
            info: 'bg-info'
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

$(document).ready(() => {
    new PaymentHandler();
});
