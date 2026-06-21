// ============================================
// COURSE LEARN PAGE - JAVASCRIPT
// ============================================

let player = null;
let currentProgress = 0;
let watchedTime = 0;
let totalTime = 0;
let isCompleted = false;

// ? FLASHCARD STATE MANAGEMENT
let flashcardStates = {}; // Store state for each flashcard set

// ? TEST STATE MANAGEMENT
let testStates = {}; // Store state for each test
let testTimers = {}; // Store timers for each test

// ? THEORY CONTENT TRACKING
let theoryContentObserver = null;

// Initialize on page load
document.addEventListener('DOMContentLoaded', function() {
    initializeVideoPlayer();
    setupLessonNavigation();
    setupMarkComplete();
    loadCourseProgress();
    setupFlashcardToggles(); // ? Setup flashcard toggle buttons
    setupTestToggles(); // ? Setup test toggle buttons
    setupTheoryTracking(); // ? Track theory content viewing
    
    // ? Listen for storage events (when user completes content in another tab)
    window.addEventListener('storage', function(e) {
        if (e.key === 'courseProgressUpdated') {
            loadCourseProgress();
            // Clear the flag
            localStorage.removeItem('courseProgressUpdated');
        }
    });
    
    // ? Reload progress when window regains focus (user comes back from flashcard)
    window.addEventListener('focus', function() {
        loadCourseProgress();
    });
});

// ============================================
// THEORY CONTENT TRACKING
// ============================================

function setupTheoryTracking() {
    const theoryItems = document.querySelectorAll('.theory-item');
    
    theoryItems.forEach(item => {
        // If the item contains a PDF document, do not auto-track it via scrolling
        if (item.querySelector('.pdf-viewer-section')) {
            return;
        }

        const contentPreview = item.querySelector('.content-preview');
        if (!contentPreview) return;
        
        // ? Use Intersection Observer to detect when theory content is viewed
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    // User has scrolled to and is viewing this theory content
                    const contentItem = entry.target.closest('.theory-item');
                    const contentId = extractTheoryContentId(contentItem);
                    
                    if (contentId) {
                        // ? Wait 3 seconds before marking as viewed
                        setTimeout(() => {
                            markTheoryContentViewed(contentId);
                        }, 3000);
                    }
                    
                    // Stop observing after marking
                    observer.unobserve(entry.target);
                }
            });
        }, {
            threshold: 0.5, // Content is 50% visible
            rootMargin: '0px'
        });
        
        observer.observe(contentPreview);
    });
}

function extractTheoryContentId(theoryElement) {
    // ? Theory content ID might be in data attribute or need to be extracted from DOM
    // This depends on how you render theory items. You may need to add data-content-id attribute
    const contentHeader = theoryElement.closest('.content-item');
    if (contentHeader) {
        // Try to find content ID from nearby elements or data attributes
        const contentId = contentHeader.dataset.contentId;
        return contentId ? parseInt(contentId) : null;
    }
    return null;
}

async function markTheoryContentViewed(contentId) {
    const urlParams = new URLSearchParams(window.location.search);
    const lessonId = urlParams.get('lessonId');
    const courseSlug = window.location.pathname.split('/')[2];
    
    if (!lessonId || !courseSlug || !contentId) return;
    
    try {
        const response = await fetch('/api/course-progress/mark-content-complete', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                courseSlug: courseSlug,
                lessonId: parseInt(lessonId),
                contentId: contentId,
                contentType: 'Theory'
            })
        });
        
        const data = await response.json();
        
        if (data.success) {
            console.log('Theory content marked as viewed:', contentId);
            // Update progress bar
            loadCourseProgress();
        }
    } catch (error) {
        console.error('Error marking theory content:', error);
    }
}

async function markPdfAsRead(button, contentId) {
    const urlParams = new URLSearchParams(window.location.search);
    const lessonId = urlParams.get('lessonId');
    const courseSlug = window.location.pathname.split('/')[2];
    
    if (!lessonId || !courseSlug || !contentId) return;
    
    button.disabled = true;
    const originalHtml = button.innerHTML;
    button.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Đang ghi nhận...';
    
    try {
        const response = await fetch('/api/course-progress/mark-content-complete', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                courseSlug: courseSlug,
                lessonId: parseInt(lessonId),
                contentId: contentId,
                contentType: 'Theory'
            })
        });
        
        const data = await response.json();
        
        if (data.success) {
            if (typeof toastr !== 'undefined') {
                toastr.success('Đã hoàn thành đọc tài liệu!');
            }
            button.className = 'btn btn-success rounded-pill px-4 py-2 mark-pdf-read-btn disabled';
            button.innerHTML = '<i class="fas fa-check-circle me-2"></i>Đã đọc xong tài liệu';
            loadCourseProgress();
        } else {
            if (typeof toastr !== 'undefined') {
                toastr.error(data.message || 'Không thể đánh dấu hoàn thành');
            }
            button.disabled = false;
            button.innerHTML = originalHtml;
        }
    } catch (error) {
        console.error('Error marking PDF content:', error);
        if (typeof toastr !== 'undefined') {
            toastr.error('Đã xảy ra lỗi kết nối');
        }
        button.disabled = false;
        button.innerHTML = originalHtml;
    }
}

// ============================================
// TEST FUNCTIONALITY
// ============================================

function setupTestToggles() {
    const toggleButtons = document.querySelectorAll('.test-toggle-btn');
    
    toggleButtons.forEach(button => {
        button.addEventListener('click', function() {
            const contentId = this.dataset.contentId;
            const testId = this.dataset.testId;
            const expandableSection = document.getElementById(`test-expandable-${contentId}`);
            
            if (expandableSection.style.display === 'none') {
                // Open test section
                expandableSection.style.display = 'block';
                this.innerHTML = '<i class="fas fa-times me-2"></i>Đóng';
                this.classList.add('active');
                this.classList.remove('btn-success');
                this.classList.add('btn-danger');
                
                // Load test if not loaded yet
                if (!testStates[contentId]) {
                    loadTest(contentId, testId);
                }
                
                // Scroll to test section
                setTimeout(() => {
                    expandableSection.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                }, 100);
            } else {
                // Close test section
                expandableSection.style.display = 'none';
                this.innerHTML = '<i class="fas fa-pencil-alt me-2"></i>Làm bài kiểm tra';
                this.classList.remove('active');
                this.classList.add('btn-success');
                this.classList.remove('btn-danger');
                
                // Stop timer if running
                if (testTimers[contentId]) {
                    clearInterval(testTimers[contentId]);
                    delete testTimers[contentId];
                }
            }
        });
    });
}

async function loadTest(contentId, testId) {
    const expandableSection = document.getElementById(`test-expandable-${contentId}`);
    const loadingDiv = expandableSection.querySelector('.test-loading');
    const playerDiv = expandableSection.querySelector('.test-player');
    
    try {
        // Fetch test data
        const response = await fetch(`/api/TestApi/${testId}`);
        const data = await response.json();
        
        if (data.success && data.questions && data.questions.length > 0) {
            // Initialize test state
            testStates[contentId] = {
                testId: testId,
                test: data.test,
                questions: data.questions,
                answers: {},
                startTime: Date.now()
            };
            
            // Hide loading, show player
            loadingDiv.style.display = 'none';
            playerDiv.style.display = 'block';
            
            // Render test info
            renderTestInfo(contentId);
            
            // Render all questions
            renderQuestions(contentId);
            
            // Start timer if time limit exists
            if (data.test.timeLimitSec) {
                startTestTimer(contentId, data.test.timeLimitSec);
            }
        } else {
            loadingDiv.innerHTML = `
                <i class="fas fa-exclamation-triangle fa-3x text-warning mb-3"></i>
                <p>${data.message || 'Bài kiểm tra này chưa có câu hỏi nào.'}</p>
            `;
        }
    } catch (error) {
        console.error('Error loading test:', error);
        loadingDiv.innerHTML = `
            <i class="fas fa-exclamation-circle fa-3x text-danger mb-3"></i>
            <p>Không thể tải bài kiểm tra. Vui lòng thử lại.</p>
        `;
    }
}

function renderTestInfo(contentId) {
    const state = testStates[contentId];
    if (!state) return;
    
    const titleElement = document.getElementById(`testTitle-${contentId}`);
    const timeElement = document.getElementById(`testTime-${contentId}`);
    const questionCountElement = document.getElementById(`testQuestionCount-${contentId}`);
    
    if (titleElement) titleElement.textContent = state.test.title;
    if (timeElement && state.test.timeLimitSec) {
        timeElement.textContent = `${Math.floor(state.test.timeLimitSec / 60)} phút`;
    }
    if (questionCountElement) {
        questionCountElement.textContent = `${state.questions.length} câu hỏi`;
    }
}

function renderQuestions(contentId) {
    const state = testStates[contentId];
    if (!state) return;
    
    const container = document.getElementById(`questionsContainer-${contentId}`);
    if (!container) return;
    
    container.innerHTML = '';
    
    state.questions.forEach((question, index) => {
        const questionDiv = document.createElement('div');
        questionDiv.className = 'question-item mb-4 p-4 border rounded bg-white';
        
        let optionsHtml = '';
        const inputType = question.type === 'MCQ_Multi' ? 'checkbox' : 'radio';
        const inputName = `question-${question.questionId}`;
        
        question.options.forEach(option => {
            optionsHtml += `
                <div class="form-check mb-2">
                    <input class="form-check-input" 
                           type="${inputType}" 
                           name="${inputName}" 
                           id="option-${option.optionId}" 
                           value="${option.optionId}"
                           onchange="handleAnswerChange('${contentId}', ${question.questionId}, this)">
                    <label class="form-check-label" for="option-${option.optionId}">
                        ${option.optionText}
                    </label>
                </div>
            `;
        });
        
        questionDiv.innerHTML = `
            <div class="d-flex justify-content-between align-items-start mb-3">
                <h5 class="mb-0">Câu ${index + 1}</h5>
                <span class="badge bg-info">${question.points} điểm</span>
            </div>
            <p class="mb-3">${question.stemText}</p>
            <div class="options-container">
                ${optionsHtml}
            </div>
        `;
        
        container.appendChild(questionDiv);
    });
}

function handleAnswerChange(contentId, questionId, input) {
    const state = testStates[contentId];
    if (!state) return;
    
    const inputType = input.type;
    
    if (inputType === 'radio') {
        // Single choice
        state.answers[questionId] = {
            selectedOptionId: parseInt(input.value)
        };
    } else {
        // Multiple choice
        const checkboxes = document.querySelectorAll(`input[name="${input.name}"]:checked`);
        const selectedIds = Array.from(checkboxes).map(cb => parseInt(cb.value));
        state.answers[questionId] = {
            selectedOptionIds: selectedIds
        };
    }
}

function startTestTimer(contentId, timeLimitSec) {
    const state = testStates[contentId];
    if (!state) return;
    
    let remainingTime = timeLimitSec;
    
    // Create timer display if not exists
    const testHeader = document.querySelector(`#test-expandable-${contentId} .test-header`);
    if (testHeader && !testHeader.querySelector('.timer-display')) {
        const timerDiv = document.createElement('div');
        timerDiv.className = 'timer-display mt-2';
        timerDiv.innerHTML = `
            <span class="badge bg-warning text-dark fs-6">
                <i class="fas fa-clock me-1"></i>
                <span id="timer-${contentId}">${formatTime(remainingTime)}</span>
            </span>
        `;
        testHeader.appendChild(timerDiv);
    }
    
    testTimers[contentId] = setInterval(() => {
        remainingTime--;
        
        const timerElement = document.getElementById(`timer-${contentId}`);
        if (timerElement) {
            timerElement.textContent = formatTime(remainingTime);
            
            // Change color when time is running out
            if (remainingTime <= 60) {
                timerElement.parentElement.classList.remove('bg-warning', 'text-dark');
                timerElement.parentElement.classList.add('bg-danger', 'text-white');
            }
        }
        
        if (remainingTime <= 0) {
            clearInterval(testTimers[contentId]);
            delete testTimers[contentId];
            
            toastr.warning('Hết giờ làm bài!');
            submitTest(contentId);
        }
    }, 1000);
}

function formatTime(seconds) {
    const minutes = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${minutes.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
}

async function submitTest(contentId) {
    const state = testStates[contentId];
    if (!state) return;
    
    // Stop timer
    if (testTimers[contentId]) {
        clearInterval(testTimers[contentId]);
        delete testTimers[contentId];
    }
    
    // Calculate time spent
    const timeSpentSec = Math.floor((Date.now() - state.startTime) / 1000);
    
    // Prepare answers
    const answers = state.questions.map(q => {
        const answer = state.answers[q.questionId];
        return {
            questionId: q.questionId,
            selectedOptionId: answer?.selectedOptionId || null,
            selectedOptionIds: answer?.selectedOptionIds || null
        };
    });
    
    // Disable submit button
    const submitBtn = document.getElementById(`btnSubmitTest-${contentId}`);
    if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Đang chấm bài...';
    }
    
    try {
        const response = await fetch('/api/TestApi/submit', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                testId: state.testId,
                timeSpentSec: timeSpentSec,
                answers: answers
            })
        });
        
        const data = await response.json();
        
        if (data.success) {
            // Show result
            showTestResult(contentId, data);
            
            // Mark test as completed in course progress
            const expandableSection = document.getElementById(`test-expandable-${contentId}`);
            const courseSlug = expandableSection.dataset.courseSlug;
            const lessonId = expandableSection.dataset.lessonId;
            const testContentId = expandableSection.dataset.contentId;
			
            await markTestComplete(courseSlug, lessonId, testContentId, data.score);
        } else {
            toastr.error(data.message || 'Không thể nộp bài');
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.innerHTML = '<i class="fas fa-paper-plane me-2"></i>Nộp bài';
            }
        }
    } catch (error) {
        console.error('Error submitting test:', error);
        toastr.error('Có lỗi xảy ra khi nộp bài');
        if (submitBtn) {
            submitBtn.disabled = false;
            submitBtn.innerHTML = '<i class="fas fa-paper-plane me-2"></i>Nộp bài';
        }
    }
}

function showTestResult(contentId, result) {
    const container = document.getElementById(`questionsContainer-${contentId}`);
    if (!container) return;
    
    const isPassed = result.percentage >= 60;
    
    // ✅ FIX: Decode HTML entities và ensure UTF-8 encoding
    const decodeHtml = (html) => {
        const txt = document.createElement('textarea');
        txt.innerHTML = html;
        return txt.value;
    };
    
    // ✅ Safely decode message
    const safeMessage = decodeHtml(result.message || (isPassed ? 'Chúc mừng! Bạn đã vượt qua bài kiểm tra' : 'Bạn chưa đạt yêu cầu'));
    
    container.innerHTML = `
        <div class="test-result text-center py-5">
            <div class="result-icon mb-4">
                <i class="fas ${isPassed ? 'fa-check-circle text-success' : 'fa-times-circle text-danger'}" 
                   style="font-size: 5rem;"></i>
            </div>
            <h3 class="${isPassed ? 'text-success' : 'text-danger'} mb-3">
                ${safeMessage}
            </h3>
            <div class="score-display mb-4">
                <h1 class="display-3 fw-bold mb-0">
                    ${result.correctAnswers} / ${result.totalQuestions}
                </h1>
                <p class="text-muted fs-5">Câu đúng</p>
                <p class="text-muted fs-6">Điểm: ${result.score}/${result.maxScore} (${result.percentage}%)</p>
            </div>
            <button class="btn btn-primary btn-lg" onclick="location.reload()">
                <i class="fas fa-redo me-2"></i>Làm lại
            </button>
        </div>
    `;
    
    // Hide submit button section
    const submitSection = document.querySelector(`#test-expandable-${contentId} .test-submit-section`);
    if (submitSection) {
        submitSection.style.display = 'none';
    }
}

async function markTestComplete(courseSlug, lessonId, contentId, score) {
    try {
        const response = await fetch('/api/course-progress/mark-content-complete', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                courseSlug: courseSlug,
                lessonId: parseInt(lessonId),
                contentId: parseInt(contentId),
                contentType: 'Test'
            })
        });
        
        const data = await response.json();
        
        if (data.success) {
            console.log('Test marked as complete:', contentId);
            // Update progress bar
            loadCourseProgress();
        }
    } catch (error) {
        console.error('Error marking test complete:', error);
    }
}

// Make functions globally accessible
window.submitTest = submitTest;
window.handleAnswerChange = handleAnswerChange;

// ============================================
// FLASHCARD FUNCTIONALITY (EXISTING CODE)
// ============================================

function setupFlashcardToggles() {
    const toggleButtons = document.querySelectorAll('.flashcard-toggle-btn');
    
    toggleButtons.forEach(button => {
        button.addEventListener('click', function() {
            const contentId = this.dataset.contentId;
            const flashcardSetId = this.dataset.flashcardSetId;
            const expandableSection = document.getElementById(`flashcard-expandable-${contentId}`);
            
            if (expandableSection.style.display === 'none') {
                // Open flashcard section
                expandableSection.style.display = 'block';
                this.innerHTML = '<i class="fas fa-times me-2"></i>Đóng';
                this.classList.add('active');
                this.classList.remove('btn-primary');
                this.classList.add('btn-danger');
                
                // Load flashcards if not loaded yet
                if (!flashcardStates[contentId]) {
                    loadFlashcardSet(contentId, flashcardSetId);
                }
                
                // Scroll to flashcard section
                setTimeout(() => {
                    expandableSection.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                }, 100);
            } else {
                // Close flashcard section
                expandableSection.style.display = 'none';
                this.innerHTML = '<i class="fas fa-play me-2"></i>Luyện tập Flashcard';
                this.classList.remove('active');
                this.classList.add('btn-primary');
                this.classList.remove('btn-danger');
            }
        });
    });
}

async function loadFlashcardSet(contentId, flashcardSetId) {
    const expandableSection = document.getElementById(`flashcard-expandable-${contentId}`);
    const loadingDiv = expandableSection.querySelector('.flashcard-loading');
    const playerDiv = expandableSection.querySelector('.flashcard-player');
    
    try {
        // Fetch flashcards
        const response = await fetch(`/api/flashcards/${flashcardSetId}`);
        const data = await response.json();
        
        if (data.success && data.flashcards && data.flashcards.length > 0) {
            // Initialize flashcard state
            flashcardStates[contentId] = {
                flashcards: data.flashcards,
                currentIndex: 0,
                isFlipped: false,
                totalCards: data.flashcards.length
            };
            
            // Hide loading, show player
            loadingDiv.style.display = 'none';
            playerDiv.style.display = 'block';
            
            // Render first card
            renderFlashcard(contentId);
            updateFlashcardCounter(contentId);
            
            // Setup keyboard navigation (Space to flip, Arrow keys to navigate)
            setupFlashcardKeyboard(contentId);
            
            // Setup card click to flip
            const flashcard = document.getElementById(`flashcard-${contentId}`);
            flashcard.addEventListener('click', () => toggleFlip(contentId));
            
            // Check if this flashcard set is already completed
            const courseSlug = expandableSection.dataset.courseSlug;
            const lessonId = expandableSection.dataset.lessonId;
            await checkFlashcardCompletion(contentId, courseSlug, lessonId);
        } else {
            loadingDiv.innerHTML = `
                <i class="fas fa-exclamation-triangle fa-3x text-warning mb-3"></i>
                <p>Bộ flashcard này chưa có thẻ nào.</p>
            `;
        }
    } catch (error) {
        console.error('Error loading flashcards:', error);
        loadingDiv.innerHTML = `
            <i class="fas fa-exclamation-circle fa-3x text-danger mb-3"></i>
            <p>Không thể tải flashcards. Vui lòng thử lại.</p>
        `;
    }
}

async function checkFlashcardCompletion(contentId, courseSlug, lessonId) {
    try {
        const response = await fetch(`/api/course-progress/check-content-completion?courseSlug=${courseSlug}&lessonId=${lessonId}&contentId=${contentId}`);
        const data = await response.json();
        
        if (data.success && data.isCompleted) {
            // Show complete button in completed state
            const completeSection = document.getElementById(`completeSection-${contentId}`);
            const completeBtn = document.getElementById(`btnComplete-${contentId}`);
            
            if (completeSection && completeBtn) {
                completeSection.style.display = 'block';
                completeSection.classList.add('show-complete');
                completeBtn.disabled = true;
                completeBtn.innerHTML = '<i class="fas fa-check-circle me-2"></i>Đã hoàn thành';
                completeBtn.style.background = 'linear-gradient(135deg, #2e7d32 0%, #43a047 100%)';
            }
        }
    } catch (error) {
        console.error('Error checking flashcard completion:', error);
    }
}

function renderFlashcard(contentId) {
    const state = flashcardStates[contentId];
    if (!state) return;
    
    const currentCard = state.flashcards[state.currentIndex];
    const frontText = document.getElementById(`frontText-${contentId}`);
    const backText = document.getElementById(`backText-${contentId}`);
    const flashcard = document.getElementById(`flashcard-${contentId}`);
    
    // Update text
    frontText.textContent = currentCard.frontText || 'Không có nội dung';
    backText.textContent = currentCard.backText || 'Không có nội dung';
    
    // Reset flip state
    flashcard.classList.remove('flipped');
    state.isFlipped = false;
    
    // Update navigation buttons
    updateNavigationButtons(contentId);
}

function toggleFlip(contentId) {
    const state = flashcardStates[contentId];
    if (!state) return;
    
    const flashcard = document.getElementById(`flashcard-${contentId}`);
    flashcard.classList.toggle('flipped');
    state.isFlipped = !state.isFlipped;
}

function previousCard(contentId) {
    const state = flashcardStates[contentId];
    if (!state || state.currentIndex === 0) return;
    
    state.currentIndex--;
    renderFlashcard(contentId);
    updateFlashcardCounter(contentId);
    
    // Hide complete button if not on last card anymore
    if (state.currentIndex < state.totalCards - 1) {
        const completeSection = document.getElementById(`completeSection-${contentId}`);
        if (completeSection) {
            completeSection.classList.remove('show-complete');
        }
    }
}

function nextCard(contentId) {
    const state = flashcardStates[contentId];
    if (!state || state.currentIndex >= state.totalCards - 1) return;
    
    state.currentIndex++;
    renderFlashcard(contentId);
    updateFlashcardCounter(contentId);
    
    // Show complete button if reached the last card
    if (state.currentIndex === state.totalCards - 1) {
        const completeSection = document.getElementById(`completeSection-${contentId}`);
        if (completeSection) {
            // Remove any inline display style first
            completeSection.removeAttribute('style');
            
            // Add show-complete class with slight delay for animation
            setTimeout(() => {
                completeSection.classList.add('show-complete');
                
                // Add a subtle shake animation to draw attention
                const tempStyle = completeSection.getAttribute('style') || '';
                completeSection.setAttribute('style', tempStyle + ' animation: shake 0.5s ease-in-out;');
                setTimeout(() => {
                    // Remove only the animation style
                    completeSection.setAttribute('style', tempStyle);
                }, 500);
            }, 300);
        }
    }
}

// Add shake animation keyframes dynamically if not exists
if (!document.querySelector('#shake-keyframes')) {
    const style = document.createElement('style');
    style.id = 'shake-keyframes';
    style.textContent = `
        @keyframes shake {
            0%, 100% { transform: translateX(0) scale(1); }
            10%, 30%, 50%, 70%, 90% { transform: translateX(-5px) scale(1); }
            20%, 40%, 60%, 80% { transform: translateX(5px) scale(1); }
        }
    `;
    document.head.appendChild(style);
}

function updateFlashcardCounter(contentId) {
    const state = flashcardStates[contentId];
    if (!state) return;
    
    const counter = document.getElementById(`cardCounter-${contentId}`);
    counter.textContent = `${state.currentIndex + 1} / ${state.totalCards} Flashcards`;
}

function updateNavigationButtons(contentId) {
    const state = flashcardStates[contentId];
    if (!state) return;
    
    const prevBtn = document.getElementById(`prevBtn-${contentId}`);
    const nextBtn = document.getElementById(`nextBtn-${contentId}`);
    
    prevBtn.disabled = state.currentIndex === 0;
    nextBtn.disabled = state.currentIndex >= state.totalCards - 1;
}

function setupFlashcardKeyboard(contentId) {
    // Remove existing listener if any
    if (window.flashcardKeyboardHandler) {
        document.removeEventListener('keydown', window.flashcardKeyboardHandler);
    }
    
    window.flashcardKeyboardHandler = function(e) {
        // Only handle if this flashcard set is visible
        const expandableSection = document.getElementById(`flashcard-expandable-${contentId}`);
        if (!expandableSection || expandableSection.style.display === 'none') return;
        
        if (e.code === 'Space') {
            e.preventDefault();
            toggleFlip(contentId);
        } else if (e.code === 'ArrowLeft') {
            e.preventDefault();
            previousCard(contentId);
        } else if (e.code === 'ArrowRight') {
            e.preventDefault();
            nextCard(contentId);
        }
    };
    
    document.addEventListener('keydown', window.flashcardKeyboardHandler);
}

async function completeFlashcardSet(contentId) {
    const expandableSection = document.getElementById(`flashcard-expandable-${contentId}`);
    const courseSlug = expandableSection.dataset.courseSlug;
    const lessonId = expandableSection.dataset.lessonId;
    const flashcardContentId = expandableSection.dataset.contentId;
    
    // Get complete button
    const completeBtn = document.getElementById(`btnComplete-${contentId}`);
    if (!completeBtn) return;
    
    // Disable button and show loading
    completeBtn.disabled = true;
    completeBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Đang xử lý...';
    
    try {
        // ? FIX: Use mark-content-complete endpoint for flashcards
        const response = await fetch('/api/course-progress/mark-content-complete', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                courseSlug: courseSlug,
                lessonId: parseInt(lessonId),
                contentId: parseInt(flashcardContentId),
                contentType: 'FlashcardSet'
            })
        });
        
        const data = await response.json();
        
        if (data.success) {
            // Show success animation
            completeBtn.innerHTML = '<i class="fas fa-check-circle me-2"></i>Đã hoàn thành!';
            completeBtn.style.background = 'linear-gradient(135deg, #2e7d32 0%, #43a047 100%)';
            
            // Show success message
            const completeSection = document.getElementById(`completeSection-${contentId}`);
            if (completeSection) {
                const successMsg = document.createElement('div');
                successMsg.className = 'success-message show';
                successMsg.innerHTML = '<i class="fas fa-trophy me-2"></i>Chúc mừng! Bạn đã hoàn thành bộ flashcard này!';
                completeSection.appendChild(successMsg);
            }
            
            // Show toastr notification
            toastr.success('Đã hoàn thành bộ flashcard!', 'Xuất sắc!');
            
            // Update progress bar
            loadCourseProgress();
            
            // Add confetti effect (optional)
            if (typeof confetti !== 'undefined') {
                confetti({
                    particleCount: 100,
                    spread: 70,
                    origin: { y: 0.6 }
                });
            }
        } else {
            // Reset button on error
            completeBtn.disabled = false;
            completeBtn.innerHTML = '<i class="fas fa-check-circle me-2"></i>Hoàn thành';
            toastr.error(data.message || 'Không thể đánh dấu hoàn thành');
        }
    } catch (error) {
        console.error('Error completing flashcard set:', error);
        
        // Reset button on error
        completeBtn.disabled = false;
        completeBtn.innerHTML = '<i class="fas fa-check-circle me-2"></i>Hoàn thành';
        toastr.error('Có lỗi xảy ra');
    }
}

// Make functions globally accessible
window.previousCard = previousCard;
window.nextCard = nextCard;
window.completeFlashcardSet = completeFlashcardSet;

// ============================================
// VIDEO PLAYER (EXISTING CODE)
// ============================================

// Initialize Video.js player
function initializeVideoPlayer() {
    const videoElement = document.getElementById('videoPlayer');
    if (!videoElement) return;

    player = videojs('videoPlayer', {
        controls: true,
        autoplay: false,
        preload: 'auto',
        fluid: true,
        aspectRatio: '16:9'
    });

    // Track video progress
    player.on('timeupdate', function() {
        watchedTime = player.currentTime();
        totalTime = player.duration();
        
        if (totalTime > 0) {
            currentProgress = (watchedTime / totalTime) * 100;
            
            // Save progress periodically (every 5 seconds)
            if (Math.floor(watchedTime) % 5 === 0) {
                saveVideoProgress();
            }
        }
    });

    player.on('ended', function() {
        // Mark as completed when video ends
        markLessonComplete();
    });
}

// Setup lesson navigation buttons
function setupLessonNavigation() {
    const prevBtn = document.getElementById('prevLessonBtn');
    const nextBtn = document.getElementById('nextLessonBtn');

    if (prevBtn) {
        prevBtn.addEventListener('click', function() {
            navigateToPreviousLesson();
        });
    }

    if (nextBtn) {
        nextBtn.addEventListener('click', function() {
            navigateToNextLesson();
        });
    }
}

// Navigate to previous lesson
function navigateToPreviousLesson() {
    const currentLessonLink = document.querySelector('.lesson-list-item.current-lesson');
    if (!currentLessonLink) return;

    const allLessons = Array.from(document.querySelectorAll('.lesson-list-item'));
    const currentIndex = allLessons.indexOf(currentLessonLink);
    
    if (currentIndex > 0) {
        const prevLesson = allLessons[currentIndex - 1];
        const prevLink = prevLesson.querySelector('.lesson-link');
        if (prevLink) {
            window.location.href = prevLink.href;
        }
    } else {
        toastr.info('Đây là bài học đầu tiên');
    }
}

// Navigate to next lesson
function navigateToNextLesson() {
    const currentLessonLink = document.querySelector('.lesson-list-item.current-lesson');
    if (!currentLessonLink) return;

    const allLessons = Array.from(document.querySelectorAll('.lesson-list-item'));
    const currentIndex = allLessons.indexOf(currentLessonLink);
    
    if (currentIndex < allLessons.length - 1) {
        const nextLesson = allLessons[currentIndex + 1];
        const nextLink = nextLesson.querySelector('.lesson-link');
        if (nextLink) {
            window.location.href = nextLink.href;
        }
    } else {
        toastr.success('Bạn đã hoàn thành tất cả bài học!');
    }
}

// Setup mark complete button
function setupMarkComplete() {
    const markCompleteBtn = document.getElementById('markCompleteBtn');
    if (markCompleteBtn) {
        markCompleteBtn.addEventListener('click', function() {
            markLessonComplete();
        });
    }
}

// Mark lesson as complete
function markLessonComplete() {
    if (isCompleted) {
        toastr.info('Bài học này đã được đánh dấu hoàn thành');
        return;
    }

    // Get lesson info from URL
    const urlParams = new URLSearchParams(window.location.search);
    const lessonId = urlParams.get('lessonId');
    const courseSlug = window.location.pathname.split('/')[2];

    if (!lessonId || !courseSlug) return;

    // Call API to mark complete
    fetch('/api/course-progress/mark-complete', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            courseSlug: courseSlug,
            lessonId: parseInt(lessonId),
            watchedDuration: Math.floor(watchedTime)
        })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            isCompleted = true;
            toastr.success('Đã đánh dấu hoàn thành bài học!');
            
            // Update UI
            const currentLessonItem = document.querySelector('.lesson-list-item.current-lesson');
            if (currentLessonItem) {
                currentLessonItem.classList.add('completed');
                const checkIcon = currentLessonItem.querySelector('.lesson-check i');
                if (checkIcon) {
                    checkIcon.className = 'fas fa-check-circle text-success';
                }
            }
            
            // Update progress bar
            loadCourseProgress();
            
            // Update mark complete button
            const markCompleteBtn = document.getElementById('markCompleteBtn');
            if (markCompleteBtn) {
                markCompleteBtn.innerHTML = '<i class="fas fa-check-circle"></i>';
                markCompleteBtn.title = 'Đã hoàn thành';
            }
        } else {
            toastr.error(data.message || 'Không thể đánh dấu hoàn thành');
        }
    })
    .catch(error => {
        console.error('Error marking complete:', error);
        toastr.error('Có lỗi xảy ra');
    });
}

// Save video progress
function saveVideoProgress() {
    const urlParams = new URLSearchParams(window.location.search);
    const lessonId = urlParams.get('lessonId');
    const courseSlug = window.location.pathname.split('/')[2];

    if (!lessonId || !courseSlug) return;

    fetch('/api/course-progress/save-progress', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            courseSlug: courseSlug,
            lessonId: parseInt(lessonId),
            watchedDuration: Math.floor(watchedTime),
            totalDuration: Math.floor(totalTime)
        })
    })
    .catch(error => {
        console.error('Error saving progress:', error);
    });
}

// Load course progress
function loadCourseProgress() {
    const courseSlug = window.location.pathname.split('/')[2];
    if (!courseSlug) return;

    fetch(`/api/course-progress/get-progress?courseSlug=${courseSlug}`)
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // ? Update progress bar with detailed info
                const progressBar = document.getElementById('courseProgressBar');
                if (progressBar) {
                    const percentage = data.completionPercentage || 0;
                    progressBar.style.width = percentage + '%';
                    
                    // ? Show completion count in progress bar
                    const progressText = progressBar.querySelector('span');
                    if (progressText) {
                        progressText.textContent = `${Math.round(percentage)}% (${data.completedContents}/${data.totalContents})`;
                    }
                }
                
                // Update completed content buttons (PDF buttons)
                if (data.completedContentIds && data.completedContentIds.length > 0) {
                    data.completedContentIds.forEach(contentId => {
                        const btn = document.querySelector(`.mark-pdf-read-btn[data-content-id="${contentId}"]`);
                        if (btn) {
                            btn.className = 'btn btn-success rounded-pill px-4 py-2 mark-pdf-read-btn disabled';
                            btn.innerHTML = '<i class="fas fa-check-circle me-2"></i>Đã đọc xong tài liệu';
                            btn.disabled = true;
                        }
                    });
                }
                
                // Update completed lessons UI
                if (data.completedLessons && data.completedLessons.length > 0) {
                    data.completedLessons.forEach(lessonId => {
                        const lessonLink = document.querySelector(`a[href*="lessonId=${lessonId}"]`);
                        if (lessonLink) {
                            const lessonItem = lessonLink.closest('.lesson-list-item');
                            if (lessonItem) {
                                lessonItem.classList.add('completed');
                                const checkIcon = lessonItem.querySelector('.lesson-check i');
                                if (checkIcon) {
                                    checkIcon.className = 'fas fa-check-circle text-success';
                                }
                            }
                        }
                    });
                }
                
                // Check if current lesson is completed
                const urlParams = new URLSearchParams(window.location.search);
                const currentLessonId = parseInt(urlParams.get('lessonId'));
                if (data.completedLessons && data.completedLessons.includes(currentLessonId)) {
                    isCompleted = true;
                    const markCompleteBtn = document.getElementById('markCompleteBtn');
                    if (markCompleteBtn) {
                        markCompleteBtn.innerHTML = '<i class="fas fa-check-circle"></i>';
                        markCompleteBtn.title = 'Đã hoàn thành';
                    }
                }
                
                // ? Log progress info for debugging
                console.log('Course Progress:', {
                    percentage: data.completionPercentage,
                    completed: data.completedContents,
                    total: data.totalContents
                });
            }
        })
        .catch(error => {
            console.error('Error loading progress:', error);
        });
}

// Save progress before leaving page
window.addEventListener('beforeunload', function() {
    if (player && watchedTime > 0) {
        saveVideoProgress();
    }
});
