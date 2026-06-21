// ============================================
// COURSE BUILDER - JAVASCRIPT
// ============================================

let currentStep = 1;
let courseData = {
    title: '',
    slug: '',
    summary: '',
    categoryId: null,
    coverUrl: '',
    price: 0,
    isPublished: false,
    chapters: []
};

let chapterCounter = 0;
let lessonCounter = 0;
let contentCounter = 0;
let autosaveTimer = null;
let ckEditorInstances = {};
let lastSlugCheck = { value: '', available: true, loading: false };
let slugDebounceTimer = null;
let lastSlugNotice = { value: '', at: 0 };
let autoSlugging = false;

// Helper: notify by toastr (fallback to alert) and jump to Slug input (dedup)
function notifyAndFocusSlug(message, slugValue) {
    const slug = slugValue ?? (document.getElementById('Slug')?.value || '');
    const now = Date.now();
    if (lastSlugNotice.value === slug && now - lastSlugNotice.at < 2500) {
        // avoid spamming the same message repeatedly
        return;
    }
    lastSlugNotice = { value: slug, at: now };

    const msg = message || 'Slug này đã tồn tại, vui lòng chọn slug khác.';
    if (window.toastr && typeof toastr.error === 'function') {
        try { toastr.clear(); } catch { }
        toastr.error(msg, 'Thông báo');
    } else {
        try { alert(msg); } catch { /* no-op */ }
    }
    const slugInput = document.getElementById('Slug');
    if (slugInput) {
        slugInput.scrollIntoView({ behavior: 'smooth', block: 'center' });
        setTimeout(() => {
            slugInput.focus({ preventScroll: true });
            if (typeof slugInput.select === 'function') slugInput.select();
        }, 200);
    }
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', function () {
    initializeCKEditor();
    setupEventListeners();

    // Load existing data if editing
    if (typeof window.existingCourseData !== 'undefined') {
        loadExistingData(window.existingCourseData);
    }

    // Start autosave
    //startAutosave();

    // Initialize slug generation
    const titleInput = document.getElementById('Title');
    const slugInput = document.getElementById('Slug');
    if (titleInput) {
        titleInput.addEventListener('input', () => {
            generateSlug();
            // clear error while typing; re-validate with debounce
            hideFieldError('Slug');
            lastSlugCheck = { value: '', available: undefined, loading: false };
        });
    }
    if (slugInput) {
        const debouncedCheck = () => {
            const slug = slugInput.value.trim();
            if (!slug) { hideFieldError('Slug'); return; }
            clearTimeout(slugDebounceTimer);
            hideFieldError('Slug'); // clear old red text as user edits
            lastSlugCheck.loading = true;
            slugDebounceTimer = setTimeout(() => {
                checkSlugUnique(slug).then(avail => {
                    if (!avail) {
                        showFieldError('Slug', 'Slug này đã tồn tại');
                        notifyAndFocusSlug('Slug này đã tồn tại, vui lòng chọn slug khác.', slug);
                    } else {
                        // valid -> clear any previous toast
                        if (window.toastr && typeof toastr.clear === 'function') toastr.clear();
                    }
                }).catch(() => { /* no-op */ });
            }, 300);
        };
        slugInput.addEventListener('input', debouncedCheck);
        slugInput.addEventListener('blur', debouncedCheck);
    }
});
// Helper: get courseId from query when editing
function getCurrentCourseId() {
    const qs = new URLSearchParams(window.location.search);
    const id = qs.get('id');
    return id ? parseInt(id) : null;
}

// API call: check slug uniqueness (no toastr here; callers decide UI)
async function checkSlugUnique(slug) {
    const courseId = getCurrentCourseId();
    lastSlugCheck.loading = true;
    try {
        const url = `/courses/check-slug?slug=${encodeURIComponent(slug)}${courseId ? `&excludeId=${courseId}` : ''}`;
        const res = await fetch(url, { method: 'GET', credentials: 'same-origin' });
        const data = await res.json();
        lastSlugCheck = { value: slug, available: !!data.available, loading: false };
        if (data.available) {
            hideFieldError('Slug');
            if (window.toastr && typeof toastr.clear === 'function') toastr.clear();
        } else {
            showFieldError('Slug', 'Slug này đã tồn tại');
        }
        return !!data.available;
    } catch (e) {
        lastSlugCheck = { value: slug, available: true, loading: false };
        return true; // Không chặn trong trường hợp lỗi mạng tạm thời
    }
}
// ============================================
// CKEDITOR INITIALIZATION
// ============================================
function initializeCKEditor() {
    // Initialize Summary editor
    const summaryField = document.getElementById('Summary');
    if (summaryField) {
        ClassicEditor
            .create(summaryField, {
                toolbar: ['heading', '|', 'bold', 'italic', 'link', 'bulletedList', 'numberedList', '|', 'undo', 'redo'],
                placeholder: 'Nhập mô tả ngắn về khóa học...'
            })
            .then(editor => {
                ckEditorInstances['Summary'] = editor;
            })
            .catch(error => {
                console.error('Error initializing CKEditor:', error);
            });
    }
}

function initializeChapterDescriptionEditor(chapterId) {
    const textarea = document.getElementById(`chapterDesc_${chapterId}`);
    if (textarea && !ckEditorInstances[`chapterDesc_${chapterId}`]) {
        ClassicEditor
            .create(textarea, {
                toolbar: ['heading', '|', 'bold', 'italic', 'link', '|', 'undo', 'redo'],
                placeholder: 'Mô tả ngắn về chương...'
            })
            .then(editor => {
                ckEditorInstances[`chapterDesc_${chapterId}`] = editor;
            })
            .catch(error => {
                console.error('Error initializing chapter description editor:', error);
            });
    }
}

function initializeContentBodyEditor(contentId) {
    const textarea = document.getElementById(`contentBody_${contentId}`);
    if (textarea && !ckEditorInstances[`contentBody_${contentId}`]) {
        ClassicEditor
            .create(textarea, {
                toolbar: {
                    items: [
                        'heading', '|',
                        'bold', 'italic', 'link', '|',
                        'bulletedList', 'numberedList', '|',
                        'imageUpload', 'blockQuote', 'insertTable', '|',
                        'undo', 'redo', '|',
                        'code', 'codeBlock'
                    ]
                },
                placeholder: 'Nhập nội dung bài học...',
                image: {
                    toolbar: ['imageStyle:inline', 'imageStyle:block', 'imageStyle:side', '|', 'imageTextAlternative']
                }
            })
            .then(editor => {
                ckEditorInstances[`contentBody_${contentId}`] = editor;
            })
            .catch(error => {
                console.error('Error initializing content body editor:', error);
            });
    }
}

// ============================================
// EVENT LISTENERS
// ============================================
function setupEventListeners() {
    // File upload preview
    const coverFileInput = document.getElementById('CoverFile');
    if (coverFileInput) {
        coverFileInput.addEventListener('change', handleFileUpload);
    }

    // Remove image button
    const removeImageBtn = document.querySelector('.remove-image');
    if (removeImageBtn) {
        removeImageBtn.addEventListener('click', removeImage);
    }
}

// ============================================
// STEP NAVIGATION
// ============================================
async function nextStep() {
    // ✅ LƯU DỮ LIỆU CỦA LESSON HIỆN TẠI NẾU ĐANG Ở STEP 3
    if (currentStep === 3 && window.currentLesson) {
        saveCurrentLessonContents();
    }

    // Save first so validation reads the latest DOM-derived state (especially for step 2)
    saveStepData();

    if (!validateCurrentStep()) {
        return;
    }
    // Extra: step 1 must check slug uniqueness server-side
    if (currentStep === 1) {
        const slug = document.getElementById('Slug')?.value.trim();
        if (slug) {
            // Only re-check if slug changed or no previous result
            if (lastSlugCheck.value !== slug || lastSlugCheck.loading || lastSlugCheck.available !== true) {
                const available = await checkSlugUnique(slug);
                if (!available) { notifyAndFocusSlug('Slug này đã tồn tại, vui lòng chọn slug khác.', slug); return; }
            }
        }
    }

    if (currentStep < 4) {
        currentStep++;
        updateStepDisplay();

        // Special handling for step 3 - populate lesson selector
        if (currentStep === 3) {
            populateLessonSelector();
        }

        // Special handling for step 4 - show preview
        if (currentStep === 4) {
            renderPreview();
        }
    }
}

function prevStep() {
    // ✅ LƯU DỮ LIỆU CỦA LESSON HIỆN TẠI NẾU ĐANG Ở STEP 3
    if (currentStep === 3 && window.currentLesson) {
        saveCurrentLessonContents();
    }

    if (currentStep > 1) {
        saveStepData();
        currentStep--;
        updateStepDisplay();
    }
}

function updateStepDisplay() {
    // Update progress bar
    const progressFill = document.querySelector('.progress-fill');
    const progressPercentage = ((currentStep - 1) / 3) * 100;
    progressFill.style.width = `${progressPercentage}%`;

    // Update step indicators
    document.querySelectorAll('.step').forEach((step, index) => {
        const stepNum = index + 1;
        step.classList.remove('active', 'completed');

        if (stepNum === currentStep) {
            step.classList.add('active');
        } else if (stepNum < currentStep) {
            step.classList.add('completed');
        }
    });

    // Update step content
    document.querySelectorAll('.step-content').forEach(content => {
        content.classList.remove('active');
    });

    const activeContent = document.querySelector(`.step-content[data-step="${currentStep}"]`);
    if (activeContent) {
        activeContent.classList.add('active');
    }

    // Scroll to top
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

// ============================================
// STEP VALIDATION
// ============================================
function validateCurrentStep() {
    let isValid = true;

    // Clear previous errors
    document.querySelectorAll('.field-error').forEach(el => {
        el.classList.remove('show');
        el.textContent = '';
    });

    if (currentStep === 1) {
        // Validate course info
        const title = document.getElementById('Title').value.trim();
        const slug = document.getElementById('Slug').value.trim();

        if (!title) {
            showFieldError('Title', 'Tiêu đề khóa học là bắt buộc');
            isValid = false;
        }

        if (!slug) {
            showFieldError('Slug', 'Slug là bắt buộc');
            isValid = false;
        } else if (!/^[a-z0-9-]+$/.test(slug)) {
            showFieldError('Slug', 'Slug chỉ được chứa chữ thường, số và dấu gạch ngang');
            isValid = false;
        }
    }

    //step 2
    if (currentStep === 2) {
        // Validate chapters and lessons
        if (courseData.chapters.length === 0) {
            toastr.error('Vui lòng thêm ít nhất một chương');
            isValid = false;
        } else {
            let hasLesson = false;
            for (const chapter of courseData.chapters) {
                if (chapter.lessons && chapter.lessons.length > 0) {
                    hasLesson = true;
                    break;
                }
            }
            if (!hasLesson) {
                alert('Vui lòng thêm ít nhất một bài học');
                isValid = false;
            }
        }
    }

    //step 3
    if (currentStep === 3) {
        //luu noi dung hien tai truoc khi validate 
        if (window.currentLesson) {
            saveCurrentLessonContents();
        }

        const testErrors = validateStep3LessonContents();
        if (testErrors.length > 0) {
            testErrors.forEach(err => toastr.error(err));
            isValid = false;
        }
    }

    return isValid;
}

//valid step 3
function validateStep3LessonContents() {
    const errors = [];
    courseData.chapters.forEach((chapter, chapterIndex) => {
        chapter.lessons.forEach((lesson, lessonIndex) => {
            if (!lesson.contents) return;

            lesson.contents.forEach((contents, contentIndex) => {
                //kiem tra bai test
                if (contents.contentType === 'Test') {
                    const path = `Chương ${chapterIndex + 1} -> Bài ${lessonIndex + 1} -> Test ${contentIndex + 1}`;


                    //1. bai test phai co it nhat 1 cau hoi
                    if (!contents.questions || contents.questions.length === 0) {
                        errors.push(`${path} phải có ít nhất một câu hỏi.`);
                        return;
                    }

                    contents.questions.forEach((question, questionIndex) => {
                        const questionPath = `${path} -> Câu hỏi ${questionIndex + 1}`;

                        //2.cau hỏi phai co noi dung
                        if (!question.stemText || question.stemText.trim() === "") {
                            errors.push(`${questionPath} chưa có nội dung câu hỏi`)
                        }
                        //3. cau hoi phai co it nhat 2 lua chon
                        if (!question.options || question.options.length < 2) {
                            errors.push(`${questionPath} phải có ít nhất 2 đáp án`);
                            return;
                        }
                        //4. moi dap an phai co noi dung
                        question.options.forEach((option, optionIndex) => {
                            if (!option.optionText || option.optionText.trim() === "")
                                errors.push(`${questionPath} -> Đáp án ${optionIndex + 1}không có nội dung`)
                        });
                        //5. phải có ít nhất 1 đáp án đúng
                        const anyCorrect = question.options.some(option => option.isCorrect === true);
                        if (!anyCorrect) {
                            errors.push(`${questionPath} chưa có đáp án đúng nào`);
                        }
                    });
                }
            });

        });
    });
    return errors;
}


function showFieldError(fieldName, message) {
    const errorSpan = document.querySelector(`.field-error[data-field="${fieldName}"]`);
    if (errorSpan) {
        errorSpan.textContent = message;
        errorSpan.classList.add('show');
        toastr.error(message);
    }
}

function hideFieldError(fieldName) {
    const errorSpan = document.querySelector(`.field-error[data-field="${fieldName}"]`);
    if (errorSpan) {
        errorSpan.textContent = '';
        errorSpan.classList.remove('show');
    }
}

// ============================================
// SAVE STEP DATA
// ============================================
// Helper: save all contents of current lesson
function saveCurrentLessonContents() {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;

    const lesson = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex];

    lesson.contents.forEach((content, index) => {
        // Save title
        const titleInput = document.querySelector(`.content-item[data-content-index="${index}"] .content-title-input`);
        if (titleInput) {
            content.title = titleInput.value.trim();
        }

        // Save content type
        const typeSelect = document.querySelector(`.content-item[data-content-index="${index}"] .content-type-select`);
        if (typeSelect) {
            content.contentType = typeSelect.value;
        }

        // Save content based on type
        if (content.contentType === 'Theory') {
            // Save theory content from CKEditor
            for (const [key, editor] of Object.entries(ckEditorInstances)) {
                if (key.includes(`contentBody_`) && key.includes(`_${index}`)) {
                    content.body = editor.getData();
                    break;
                }
            }
        } else if (content.contentType === 'Video') {
            // Video URL đã được lưu trong handleVideoUpload
            // Không cần làm gì thêm
        } else if (content.contentType === 'FlashcardSet') {
            // Save flashcard set title and description
            const setTitleInput = document.querySelector(`.content-item[data-content-index="${index}"] .flashcard-set-title`);
            const setDescInput = document.querySelector(`.content-item[data-content-index="${index}"] .flashcard-set-desc`);
            
            if (setTitleInput) {
                content.flashcardSetTitle = setTitleInput.value.trim();
            }
            if (setDescInput) {
                content.flashcardSetDesc = setDescInput.value.trim();
            }

            // Save each flashcard from DOM
            const flashcardItems = document.querySelectorAll(`.content-item[data-content-index="${index}"] .flashcard-item`);
            if (!content.flashcards) {
                content.flashcards = [];
            }
            
            flashcardItems.forEach((flashcardEl, cardIndex) => {
                const frontText = flashcardEl.querySelector('.flashcard-front')?.value || '';
                const backText = flashcardEl.querySelector('.flashcard-back')?.value || '';
                const hint = flashcardEl.querySelector('.flashcard-hint')?.value || '';
                
                if (content.flashcards[cardIndex]) {
                    content.flashcards[cardIndex].frontText = frontText;
                    content.flashcards[cardIndex].backText = backText;
                    content.flashcards[cardIndex].hint = hint;
                    content.flashcards[cardIndex].orderIndex = cardIndex;
                }
            });
        } else if (content.contentType === 'Test') {
            // Save test title, description, time limit, max attempts
            const testTitleInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-title`);
            const testDescInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-desc`);
            const testTimeInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-time`);
            const testAttemptsInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-attempts`);
            
            if (testTitleInput) {
                content.testTitle = testTitleInput.value.trim();
            }
            if (testDescInput) {
                content.testDesc = testDescInput.value.trim();
            }
            if (testTimeInput) {
                content.timeLimitMinutes = parseInt(testTimeInput.value) || 30;
            }
            if (testAttemptsInput) {
                content.maxAttempts = parseInt(testAttemptsInput.value) || 3;
            }

            // Save each question from DOM
            const questionItems = document.querySelectorAll(`.content-item[data-content-index="${index}"] .test-question-item`);
            if (!content.questions) {
                content.questions = [];
            }
            
            questionItems.forEach((questionEl, qIndex) => {
                const questionTypeSelect = questionEl.querySelector('.form-control');
                const questionTextArea = questionEl.querySelectorAll('textarea')[0];
                const questionPointsInput = questionEl.querySelector('input[type="number"]');
                
                if (content.questions[qIndex]) {
                    if (questionTypeSelect) {
                        content.questions[qIndex].type = questionTypeSelect.value;
                    }
                    if (questionTextArea) {
                        content.questions[qIndex].stemText = questionTextArea.value.trim();
                    }
                    if (questionPointsInput) {
                        content.questions[qIndex].points = parseFloat(questionPointsInput.value) || 1;
                    }
                    content.questions[qIndex].orderIndex = qIndex;

                    // Save options
                    const optionItems = questionEl.querySelectorAll('.option-item');
                    if (!content.questions[qIndex].options) {
                        content.questions[qIndex].options = [];
                    }
                    
                    optionItems.forEach((optionEl, oIndex) => {
                        const checkbox = optionEl.querySelector('input[type="checkbox"]');
                        const textInput = optionEl.querySelector('input[type="text"]');
                        
                        if (content.questions[qIndex].options[oIndex]) {
                            if (checkbox) {
                                content.questions[qIndex].options[oIndex].isCorrect = checkbox.checked;
                            }
                            if (textInput) {
                                content.questions[qIndex].options[oIndex].optionText = textInput.value.trim();
                            }
                            content.questions[qIndex].options[oIndex].orderIndex = oIndex;
                        }
                    });
                }
            });
        }
    });
}

// Save step data
function saveStepData() {
    if (currentStep === 1) {
        // Save course info
        courseData.title = document.getElementById('Title').value.trim();
        courseData.slug = document.getElementById('Slug').value.trim();

        const summaryEditor = ckEditorInstances['Summary'];
        courseData.summary = summaryEditor ? summaryEditor.getData() : '';

        const categorySelect = document.getElementById('CategoryId');
        courseData.categoryId = categorySelect.value ? parseInt(categorySelect.value) : null;

        courseData.coverUrl = document.getElementById('CoverUrl').value;
        courseData.price = parseFloat(document.getElementById('Price').value) || 0;
        courseData.isPublished = false;
    }

    if (currentStep === 2) {
        // ✅ LƯU LẠI TẤT CẢ CONTENTS TRƯỚC KHI CẬP NHẬT
        const oldContentsMap = new Map();
        courseData.chapters.forEach((chapter, chIdx) => {
            chapter.lessons.forEach((lesson, lIdx) => {
                const key = `${chIdx}_${lIdx}`;
                if (lesson.contents && lesson.contents.length > 0) {
                    // Clone deep để tránh mất reference
                    oldContentsMap.set(key, JSON.parse(JSON.stringify(lesson.contents)));
                }
            });
        });

        // Save chapters and lessons from DOM
        courseData.chapters = [];

        document.querySelectorAll('.chapter-item').forEach((chapterEl, chapterIndex) => {
            const chapterId = chapterEl.dataset.chapterId;
            const chapterTitle = chapterEl.querySelector('.chapter-title-input').value.trim();

            const chapterDescEditor = ckEditorInstances[`chapterDesc_${chapterId}`];
            const chapterDesc = chapterDescEditor ? chapterDescEditor.getData() : '';

            const chapter = {
                chapterId: null,
                title: chapterTitle,
                description: chapterDesc,
                orderIndex: chapterIndex,
                lessons: []
            };

            chapterEl.querySelectorAll('.lesson-item').forEach((lessonEl, lessonIndex) => {
                const lessonTitle = lessonEl.querySelector('.lesson-title-input').value.trim();
                const lessonVisibility = lessonEl.querySelector('.lesson-visibility').value;

                const lesson = {
                    lessonId: null,
                    title: lessonTitle,
                    description: '',
                    orderIndex: lessonIndex,
                    visibility: lessonVisibility,
                    contents: []
                };

                // ✅ KHÔI PHỤC CONTENTS TỪ MAP
                const key = `${chapterIndex}_${lessonIndex}`;
                if (oldContentsMap.has(key)) {
                    lesson.contents = oldContentsMap.get(key);
                }

                chapter.lessons.push(lesson);
            });

            courseData.chapters.push(chapter);
        });
    }

    // ✅ THÊM LOGIC CHO STEP 3: LƯU NỘI DUNG TỪ DOM VÀ CKEDITOR
    if (currentStep === 3) {
        const currentLesson = window.currentLesson;
        if (currentLesson) {
            const lesson = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex];

            // Lưu tất cả contents từ DOM
            lesson.contents.forEach((content, index) => {
                // Lưu tiêu đề
                const titleInput = document.querySelector(`.content-item[data-content-index="${index}"] .content-title-input`);
                if (titleInput) {
                    content.title = titleInput.value.trim();
                }

                // Lưu loại nội dung
                const typeSelect = document.querySelector(`.content-item[data-content-index="${index}"] .content-type-select`);
                if (typeSelect) {
                    content.contentType = typeSelect.value;
                }

                // Lưu nội dung theo loại
                if (content.contentType === 'Theory') {
                    // Lưu nội dung lý thuyết từ CKEditor
                    // Tìm CKEditor instance theo pattern contentBody_*_index
                    for (const [key, editor] of Object.entries(ckEditorInstances)) {
                        if (key.includes(`contentBody_`) && key.includes(`_${index}`)) {
                            content.body = editor.getData();
                            break;
                        }
                    }
                } else if (content.contentType === 'Video') {
                    // Video URL đã được lưu trong handleVideoUpload
                    // Không cần làm gì thêm
                } else if (content.contentType === 'FlashcardSet') {
                    // Lưu flashcard set title và description
                    const setTitleInput = document.querySelector(`.content-item[data-content-index="${index}"] .flashcard-set-title`);
                    const setDescInput = document.querySelector(`.content-item[data-content-index="${index}"] .flashcard-set-desc`);
                    
                    if (setTitleInput) {
                        content.flashcardSetTitle = setTitleInput.value.trim();
                    }
                    if (setDescInput) {
                        content.flashcardSetDesc = setDescInput.value.trim();
                    }

                    // Lưu từng flashcard từ DOM
                    const flashcardItems = document.querySelectorAll(`.content-item[data-content-index="${index}"] .flashcard-item`);
                    if (!content.flashcards) {
                        content.flashcards = [];
                    }
                    
                    flashcardItems.forEach((flashcardEl, cardIndex) => {
                        const frontText = flashcardEl.querySelector('.flashcard-front')?.value || '';
                        const backText = flashcardEl.querySelector('.flashcard-back')?.value || '';
                        const hint = flashcardEl.querySelector('.flashcard-hint')?.value || '';
                        
                        if (content.flashcards[cardIndex]) {
                            content.flashcards[cardIndex].frontText = frontText;
                            content.flashcards[cardIndex].backText = backText;
                            content.flashcards[cardIndex].hint = hint;
                            content.flashcards[cardIndex].orderIndex = cardIndex;
                        }
                    });
                } else if (content.contentType === 'Test') {
                    // Lưu test title, description, time limit, max attempts
                    const testTitleInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-title`);
                    const testDescInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-desc`);
                    const testTimeInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-time`);
                    const testAttemptsInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-attempts`);
                    
                    if (testTitleInput) {
                        content.testTitle = testTitleInput.value.trim();
                    }
                    if (testDescInput) {
                        content.testDesc = testDescInput.value.trim();
                    }
                    if (testTimeInput) {
                        content.timeLimitMinutes = parseInt(testTimeInput.value) || 30;
                    }
                    if (testAttemptsInput) {
                        content.maxAttempts = parseInt(testAttemptsInput.value) || 3;
                    }

                    // Lưu từng question từ DOM
                    const questionItems = document.querySelectorAll(`.content-item[data-content-index="${index}"] .test-question-item`);
                    if (!content.questions) {
                        content.questions = [];
                    }
                    
                    questionItems.forEach((questionEl, qIndex) => {
                        const questionTypeSelect = questionEl.querySelector('.form-control');
                        const questionTextArea = questionEl.querySelectorAll('textarea')[0];
                        const questionPointsInput = questionEl.querySelector('input[type="number"]');
                        
                        if (content.questions[qIndex]) {
                            if (questionTypeSelect) {
                                content.questions[qIndex].type = questionTypeSelect.value;
                            }
                            if (questionTextArea) {
                                content.questions[qIndex].stemText = questionTextArea.value.trim();
                            }
                            if (questionPointsInput) {
                                content.questions[qIndex].points = parseFloat(questionPointsInput.value) || 1;
                            }
                            content.questions[qIndex].orderIndex = qIndex;

                            // Lưu options
                            const optionItems = questionEl.querySelectorAll('.option-item');
                            if (!content.questions[qIndex].options) {
                                content.questions[qIndex].options = [];
                            }
                            
                            optionItems.forEach((optionEl, oIndex) => {
                                const checkbox = optionEl.querySelector('input[type="checkbox"]');
                                const textInput = optionEl.querySelector('input[type="text"]');
                                
                                if (content.questions[qIndex].options[oIndex]) {
                                    if (checkbox) {
                                        content.questions[qIndex].options[oIndex].isCorrect = checkbox.checked;
                                    }
                                    if (textInput) {
                                        content.questions[qIndex].options[oIndex].optionText = textInput.value.trim();
                                    }
                                    content.questions[qIndex].options[oIndex].orderIndex = oIndex;
                                }
                            });
                        }
                    });
                }
            });
        }
    }
}

function findLessonInData(lessonId) {
    for (const chapter of courseData.chapters) {
        for (const lesson of chapter.lessons) {
            if (lesson.lessonId === lessonId) {
                return lesson;
            }
        }
    }
    return null;
}

// ============================================
// SLUG GENERATION
// ============================================
function generateSlug() {
    const titleInput = document.getElementById('Title');
    const slugInput = document.getElementById('Slug');

    if (!titleInput || !slugInput) return;

    // Vietnamese to ASCII mapping
    const vietnameseMap = {
        'à': 'a', 'á': 'a', 'ạ': 'a', 'ả': 'a', 'ã': 'a', 'â': 'a', 'ầ': 'a', 'ấ': 'a', 'ậ': 'a', 'ẩ': 'a', 'ẫ': 'a', 'ă': 'a', 'ằ': 'a', 'ắ': 'a', 'ặ': 'a', 'ẳ': 'a', 'ẵ': 'a',
        'è': 'e', 'é': 'e', 'ẹ': 'e', 'ẻ': 'e', 'ẽ': 'e', 'ê': 'e', 'ề': 'e', 'ế': 'e', 'ệ': 'e', 'ể': 'e', 'ễ': 'e',
        'ì': 'i', 'í': 'i', 'ị': 'i', 'ỉ': 'i', 'ĩ': 'i',
        'ò': 'o', 'ó': 'o', 'ọ': 'o', 'ỏ': 'o', 'õ': 'o', 'ô': 'o', 'ồ': 'o', 'ố': 'o', 'ộ': 'o', 'ổ': 'o', 'ỗ': 'o', 'ơ': 'o', 'ờ': 'o', 'ớ': 'o', 'ợ': 'o', 'ở': 'o', 'ỡ': 'o',
        'ù': 'u', 'ú': 'u', 'ụ': 'u', 'ủ': 'u', 'ũ': 'u', 'ư': 'u', 'ừ': 'u', 'ứ': 'u', 'ự': 'u', 'ử': 'u', 'ữ': 'u',
        'ỳ': 'y', 'ý': 'y', 'ỵ': 'y', 'ỷ': 'y', 'ỹ': 'y',
        'đ': 'd',
        'À': 'A', 'Á': 'A', 'Ạ': 'A', 'Ả': 'A', 'Ã': 'A', 'Â': 'A', 'Ầ': 'A', 'Ấ': 'A', 'Ậ': 'A', 'Ả': 'A', 'Ẫ': 'A', 'Ă': 'A', 'Ằ': 'A', 'Ắ': 'A', 'Ặ': 'A', 'Ẳ': 'A', 'Ẵ': 'A',
        'È': 'E', 'É': 'E', 'Ẹ': 'E', 'Ẻ': 'E', 'Ẽ': 'E', 'Ê': 'E', 'Ề': 'E', 'Ế': 'E', 'ệ': 'E', 'Ể': 'E', 'Ễ': 'E',
        'Ì': 'I', 'Í': 'I', 'Ị': 'I', 'Ỉ': 'I', 'Ĩ': 'I',
        'Ò': 'O', 'Ó': 'O', 'Ọ': 'O', 'Ỏ': 'O', 'Õ': 'O', 'Ô': 'O', 'Ồ': 'O', 'ố': 'O', 'Ộ': 'O', 'Ổ': 'O', 'Ỗ': 'O', 'Ơ': 'O', 'Ờ': 'O', 'Ớ': 'O', 'Ợ': 'O', 'Ở': 'O', 'Ỡ': 'O',
        'Ù': 'U', 'Ú': 'U', 'Ụ': 'U', 'Ủ': 'U', 'Ũ': 'U', 'Ư': 'U', 'Ừ': 'U', 'Ứ': 'U', 'Ự': 'U', 'Ử': 'U', 'Ữ': 'U',
        'Ỳ': 'Y', 'Ý': 'Y', 'Ỵ': 'Y', 'Ỷ': 'Y', 'Ỹ': 'Y',
        'Đ': 'D'
    };

    let slug = titleInput.value;

    // Replace Vietnamese characters
    for (let char in vietnameseMap) {
        slug = slug.replace(new RegExp(char, 'g'), vietnameseMap[char]);
    }

    // Convert to slug format
    slug = slug
        .toLowerCase()
        .replace(/[^a-z0-9\s-]/g, '') // Remove special characters
        .replace(/\s+/g, '-') // Replace spaces with hyphens
        .replace(/-+/g, '-') // Replace multiple hyphens with single hyphen
        .replace(/^-+|-+$/g, ''); // Remove leading/trailing hyphens

    //const slugInput = document.getElementById('Slug');
    slugInput.value = slug;
}

// ============================================
// FILE UPLOAD
// ============================================
function handleFileUpload(event) {
    const file = event.target.files[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = function (e) {
        const preview = document.getElementById('imagePreview');
        const img = preview.querySelector('img');

        img.src = e.target.result;
        preview.style.display = 'block';
    };

    reader.readAsDataURL(file);
}

function removeImage() {
    const preview = document.getElementById('imagePreview');
    const fileInput = document.getElementById('CoverFile');
    const coverUrlInput = document.getElementById('CoverUrl');

    preview.style.display = 'none';
    fileInput.value = '';
    coverUrlInput.value = '';
}

// ============================================
// CHAPTERS MANAGEMENT
// ============================================
function addChapter() {
    chapterCounter++;
    const chapterId = `chapter_${chapterCounter}`;

    const chapterHTML = `
        <div class="chapter-item" data-chapter-id="${chapterId}">
            <div class="chapter-header">
                <div class="chapter-header-left">
                    <i class="fas fa-grip-vertical chapter-drag-handle"></i>
                    <div class="chapter-toggle">
                        <i class="fas fa-chevron-down"></i>
                    </div>
                    <input type="text" 
                           class="chapter-title-input" 
                           placeholder="Tên chương" 
                           value="Chương ${chapterCounter}" 
                           required>
                </div>
                <div class="chapter-actions">
                    <button type="button" class="icon-btn danger" onclick="removeChapter('${chapterId}')">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
            <div class="chapter-body">
                <div class="chapter-description">
                    <label>Mô tả chương:</label>
                    <textarea id="chapterDesc_${chapterId}" class="form-control" rows="3"></textarea>
                </div>
                <div class="lessons-container" data-chapter-id="${chapterId}">
                    <!-- Lessons will be added here -->
                </div>
                <button type="button" class="btn btn-outline-primary btn-add-lesson" onclick="addLesson('${chapterId}')">
                    <i class="fas fa-plus"></i> Thêm bài học
                </button>
            </div>
        </div>
    `;

    const container = document.getElementById('chaptersContainer');
    container.insertAdjacentHTML('beforeend', chapterHTML);

    // Initialize CKEditor for chapter description
    setTimeout(() => {
        initializeChapterDescriptionEditor(chapterId);
    }, 100);

    // Add collapse/expand functionality
    const chapterItem = container.lastElementChild;
    const chapterHeader = chapterItem.querySelector('.chapter-header');
    chapterHeader.addEventListener('click', function (e) {
        if (e.target.closest('.chapter-actions')) return;
        chapterItem.classList.toggle('collapsed');
    });

    // Initialize Sortable for chapters
    initializeChaptersSortable();
}

function removeChapter(chapterId) {
    if (confirm('Bạn có chắc muốn xóa chương này?')) {
        const chapterItem = document.querySelector(`.chapter-item[data-chapter-id="${chapterId}"]`);
        if (chapterItem) {
            // Destroy CKEditor instance
            const editorId = `chapterDesc_${chapterId}`;
            if (ckEditorInstances[editorId]) {
                ckEditorInstances[editorId].destroy();
                delete ckEditorInstances[editorId];
            }

            chapterItem.remove();
        }
    }
}

function initializeChaptersSortable() {
    const container = document.getElementById('chaptersContainer');
    if (container && !container.sortableInitialized) {
        new Sortable(container, {
            animation: 150,
            handle: '.chapter-drag-handle',
            ghostClass: 'sortable-ghost',
            dragClass: 'sortable-drag'
        });
        container.sortableInitialized = true;
    }
}

// ============================================
// LESSONS MANAGEMENT
// ============================================
function addLesson(chapterId) {
    lessonCounter++;
    const lessonId = `lesson_${lessonCounter}`;

    const lessonHTML = `
        <div class="lesson-item" data-lesson-id="${lessonId}">
            <i class="fas fa-grip-vertical lesson-drag-handle"></i>
            <div class="lesson-info">
                <input type="text" 
                       class="lesson-title-input" 
                       placeholder="Tên bài học" 
                       value="Bài học ${lessonCounter}" 
                       required>
            </div>
            <select class="lesson-visibility form-select invisible">
                <option value="Course">Course</option>
                <option value="Public">Public</option>
                <option value="Private">Private</option>
            </select>
            <button type="button" class="icon-btn danger" onclick="removeLesson('${lessonId}')">
                <i class="fas fa-trash"></i>
            </button>
        </div>
    `;

    const lessonsContainer = document.querySelector(`.lessons-container[data-chapter-id="${chapterId}"]`);
    if (lessonsContainer) {
        lessonsContainer.insertAdjacentHTML('beforeend', lessonHTML);

        // Initialize Sortable for lessons
        initializeLessonsSortable(lessonsContainer);
    }
}

function removeLesson(lessonId) {
    if (confirm('Bạn có chắc muốn xóa bài học này?')) {
        const lessonItem = document.querySelector(`.lesson-item[data-lesson-id="${lessonId}"]`);
        if (lessonItem) {
            lessonItem.remove();
        }
    }
}

function initializeLessonsSortable(container) {
    if (container && !container.sortableInitialized) {
        new Sortable(container, {
            animation: 150,
            handle: '.lesson-drag-handle',
            ghostClass: 'sortable-ghost',
            dragClass: 'sortable-drag'
        });
        container.sortableInitialized = true;
    }
}

// ============================================
// LESSON CONTENTS MANAGEMENT
// ============================================

// ✅ HÀM MỚI: LƯU NỘI DUNG CỦA LESSON HIỆN TẠI
function saveCurrentLessonContents() {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;

    const lesson = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex];

    lesson.contents.forEach((content, index) => {
        // Save title
        const titleInput = document.querySelector(`.content-item[data-content-index="${index}"] .content-title-input`);
        if (titleInput) {
            content.title = titleInput.value.trim();
        }

        // Save content type
        const typeSelect = document.querySelector(`.content-item[data-content-index="${index}"] .content-type-select`);
        if (typeSelect) {
            content.contentType = typeSelect.value;
        }

        // Save content based on type
        if (content.contentType === 'Theory') {
            // Save theory content from CKEditor
            for (const [key, editor] of Object.entries(ckEditorInstances)) {
                if (key.includes(`contentBody_`) && key.includes(`_${index}`)) {
                    content.body = editor.getData();
                    break;
                }
            }
        } else if (content.contentType === 'Video') {
            // Video URL đã được lưu trong handleVideoUpload
            // Không cần làm gì thêm
        } else if (content.contentType === 'FlashcardSet') {
            // Save flashcard set title and description
            const setTitleInput = document.querySelector(`.content-item[data-content-index="${index}"] .flashcard-set-title`);
            const setDescInput = document.querySelector(`.content-item[data-content-index="${index}"] .flashcard-set-desc`);
            
            if (setTitleInput) {
                content.flashcardSetTitle = setTitleInput.value.trim();
            }
            if (setDescInput) {
                content.flashcardSetDesc = setDescInput.value.trim();
            }

            // Save each flashcard from DOM
            const flashcardItems = document.querySelectorAll(`.content-item[data-content-index="${index}"] .flashcard-item`);
            if (!content.flashcards) {
                content.flashcards = [];
            }
            
            flashcardItems.forEach((flashcardEl, cardIndex) => {
                const frontText = flashcardEl.querySelector('.flashcard-front')?.value || '';
                const backText = flashcardEl.querySelector('.flashcard-back')?.value || '';
                const hint = flashcardEl.querySelector('.flashcard-hint')?.value || '';
                
                if (content.flashcards[cardIndex]) {
                    content.flashcards[cardIndex].frontText = frontText;
                    content.flashcards[cardIndex].backText = backText;
                    content.flashcards[cardIndex].hint = hint;
                    content.flashcards[cardIndex].orderIndex = cardIndex;
                }
            });
        } else if (content.contentType === 'Test') {
            // Save test title, description, time limit, max attempts
            const testTitleInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-title`);
            const testDescInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-desc`);
            const testTimeInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-time`);
            const testAttemptsInput = document.querySelector(`.content-item[data-content-index="${index}"] .test-attempts`);
            
            if (testTitleInput) {
                content.testTitle = testTitleInput.value.trim();
            }
            if (testDescInput) {
                content.testDesc = testDescInput.value.trim();
            }
            if (testTimeInput) {
                content.timeLimitMinutes = parseInt(testTimeInput.value) || 30;
            }
            if (testAttemptsInput) {
                content.maxAttempts = parseInt(testAttemptsInput.value) || 3;
            }

            // Save each question from DOM
            const questionItems = document.querySelectorAll(`.content-item[data-content-index="${index}"] .test-question-item`);
            if (!content.questions) {
                content.questions = [];
            }
            
            questionItems.forEach((questionEl, qIndex) => {
                const questionTypeSelect = questionEl.querySelector('.form-control');
                const questionTextArea = questionEl.querySelectorAll('textarea')[0];
                const questionPointsInput = questionEl.querySelector('input[type="number"]');
                
                if (content.questions[qIndex]) {
                    if (questionTypeSelect) {
                        content.questions[qIndex].type = questionTypeSelect.value;
                    }
                    if (questionTextArea) {
                        content.questions[qIndex].stemText = questionTextArea.value.trim();
                    }
                    if (questionPointsInput) {
                        content.questions[qIndex].points = parseFloat(questionPointsInput.value) || 1;
                    }
                    content.questions[qIndex].orderIndex = qIndex;

                    // Save options
                    const optionItems = questionEl.querySelectorAll('.option-item');
                    if (!content.questions[qIndex].options) {
                        content.questions[qIndex].options = [];
                    }
                    
                    optionItems.forEach((optionEl, oIndex) => {
                        const checkbox = optionEl.querySelector('input[type="checkbox"]');
                        const textInput = optionEl.querySelector('input[type="text"]');
                        
                        if (content.questions[qIndex].options[oIndex]) {
                            if (checkbox) {
                                content.questions[qIndex].options[oIndex].isCorrect = checkbox.checked;
                            }
                            if (textInput) {
                                content.questions[qIndex].options[oIndex].optionText = textInput.value.trim();
                            }
                            content.questions[qIndex].options[oIndex].orderIndex = oIndex;
                        }
                    });
                }
            });
        }
    });
}

function populateLessonSelector() {
    // ✅ LƯU DỮ LIỆU CỦA LESSON HIỆN TẠI TRƯỚC KHI CHUYỂN
    if (window.currentLesson) {
        saveCurrentLessonContents();
    }

    saveStepData(); // Make sure we have latest data

    const selector = document.getElementById('lessonSelector');
    selector.innerHTML = '<option value="">-- Chọn bài học --</option>';

    courseData.chapters.forEach((chapter, chapterIndex) => {
        chapter.lessons.forEach((lesson, lessonIndex) => {
            const lessonKey = `${chapterIndex}_${lessonIndex}`;
            const option = document.createElement('option');
            option.value = lessonKey;
            option.textContent = `${chapter.title} → ${lesson.title}`;
            selector.appendChild(option);
        });
    });
}

function loadLessonContents() {
    // ✅ LƯU DỮ LIỆU CỦA LESSON HIỆN TẠI TRƯỚC KHI CHUYỂN
    if (window.currentLesson) {
        saveCurrentLessonContents();
    }

    const selector = document.getElementById('lessonSelector');
    const selectedKey = selector.value;

    const contentsContainer = document.getElementById('contentsContainer');
    const contentsList = document.getElementById('contentsList');

    if (!selectedKey) {
        contentsContainer.style.display = 'none';
        return;
    }

    contentsContainer.style.display = 'block';

    const [chapterIndex, lessonIndex] = selectedKey.split('_').map(Number);
    const lesson = courseData.chapters[chapterIndex].lessons[lessonIndex];

    // Store current lesson for adding contents
    window.currentLesson = { chapterIndex, lessonIndex };

    // ✅ LƯU TRẠNG THÁI MỞ/ĐÓNG CỦA CÁC CONTENT ITEMS TRƯỚC KHI RELOAD
    const openStates = new Map();
    document.querySelectorAll('.content-item').forEach((item) => {
        const index = item.dataset.contentIndex;
        const body = item.querySelector('.content-body');
        openStates.set(index, body && body.style.display !== 'none');
    });

    // Render existing contents
    contentsList.innerHTML = '';
    if (lesson.contents && lesson.contents.length > 0) {
        lesson.contents.forEach((content, index) => {
            renderContentItem(content, index);
            
            // ✅ KHÔI PHỤC TRẠNG THÁI MỞ/ĐÓNG
            if (openStates.get(String(index))) {
                setTimeout(() => {
                    const contentItem = document.querySelector(`.content-item[data-content-index="${index}"]`);
                    if (contentItem) {
                        const body = contentItem.querySelector('.content-body');
                        const icon = contentItem.querySelector('.content-header button i');
                        if (body && icon) {
                            body.style.display = 'block';
                            icon.classList.replace('fa-chevron-down', 'fa-chevron-up');
                        }
                    }
                }, 50);
            }
        });
    }
}

function addContent() {
    const currentLesson = window.currentLesson;
    if (!currentLesson) {
        alert('Vui lòng chọn bài học');
        return;
    }

    contentCounter++;
    const contentId = `content_${contentCounter}`;

    const content = {
        contentId: null,
        contentType: 'Theory',
        refId: null,
        title: `Nội dung ${contentCounter}`,
        body: '',
        videoUrl: '',
        orderIndex: courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents.length
    };

    courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents.push(content);

    const contentsList = document.getElementById('contentsList');
    renderContentItem(content, content.orderIndex);
}

function renderContentItem(content, index) {
    // ✅ TẠO ID CỐ ĐỊNH DỰA TRÊN VỊ TRÍ TRONG LESSON
    const currentLesson = window.currentLesson;
    const contentId = currentLesson
        ? `content_${currentLesson.chapterIndex}_${currentLesson.lessonIndex}_${index}`
        : `content_${Date.now()}_${index}`;

    const contentHTML = `
        <div class="content-item" data-content-index="${index}">
            <div class="content-header">
                <div>
                    <span class="content-type-badge ${content.contentType.toLowerCase()}">${getContentTypeLabel(content.contentType)}</span>
                    <strong style="margin-left: 1rem;">${content.title || 'Chưa có tiêu đề'}</strong>
                </div>
                <div>
                    <button type="button" class="icon-btn" onclick="toggleContentBody(${index})">
                        <i class="fas fa-chevron-down"></i>
                    </button>
                    <button type="button" class="icon-btn danger" onclick="removeContent(${index})">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
            <div class="content-body" style="display: none;">
                <div class="content-form">
                    <div class="form-group">
                        <label>Loại nội dung:</label>
                        <select class="form-select content-type-select" onchange="updateContentType(${index}, this.value)">
                            <option value="Theory" ${content.contentType === 'Theory' ? 'selected' : ''}>Lý thuyết</option>
                            <option value="Video" ${content.contentType === 'Video' ? 'selected' : ''}>Video</option>
                            <option value="FlashcardSet" ${content.contentType === 'FlashcardSet' ? 'selected' : ''}>Flashcard</option>
                            <option value="Test" ${content.contentType === 'Test' ? 'selected' : ''}>Test</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label>Tiêu đề:</label>
                        <input type="text" class="form-control content-title-input" value="${content.title || ''}" onchange="updateContentTitle(${index}, this.value)">
                    </div>
                    <div class="content-type-fields" data-content-index="${index}">
                        ${renderContentTypeFields(content, contentId, index)}
                    </div>
                </div>
            </div>
        </div>
    `;

    const contentsList = document.getElementById('contentsList');
    contentsList.insertAdjacentHTML('beforeend', contentHTML);

    // Initialize CKEditor for theory content
    if (content.contentType === 'Theory') {
        setTimeout(() => {
            initializeContentBodyEditor(contentId);
        }, 100);
    }
}

function renderContentTypeFields(content, contentId, index) {
    if (content.contentType === 'Theory') {
        return `
            <div class="form-group mb-3">
                <label>Nội dung văn bản (Lý thuyết):</label>
                <textarea id="contentBody_${contentId}" class="form-control" rows="10">${content.body || ''}</textarea>
            </div>
            <div class="form-group mb-3">
                <label class="form-label fw-semibold"><i class="fas fa-file-pdf text-danger me-2"></i>Tài liệu đính kèm (PDF):</label>
                <input type="file" class="form-control" accept="application/pdf"
                    onchange="handlePdfUpload(${index}, this)">
                <small class="form-text text-muted">
                    Hỗ trợ file PDF (tối đa 50MB). Tài liệu sẽ được lưu trên Google Cloud Storage.
                </small>
                ${content.documentUrl ? `
                    <div class="pdf-preview-container mt-2 p-2 border rounded bg-light d-flex align-items-center justify-content-between">
                        <div class="d-flex align-items-center">
                            <i class="fas fa-file-pdf fa-2x text-danger me-3"></i>
                            <div>
                                <a href="${content.documentUrl}" target="_blank" class="text-decoration-none fw-bold text-primary pdf-filename-link">
                                    ${content.documentUrl.substring(content.documentUrl.lastIndexOf('/') + 1)}
                                </a>
                                <div class="text-muted small">Tài liệu đã được tải lên Cloud Storage</div>
                            </div>
                        </div>
                        <button type="button" class="btn btn-outline-danger btn-sm" onclick="removePdfFile(${index})">
                            <i class="fas fa-trash-alt"></i> Xóa
                        </button>
                    </div>
                ` : ''}
            </div>
        `;
    } else if (content.contentType === 'Video') {
        return `
            <div class="form-group">
                <label>Tải video lên từ máy tính:</label>
                <input type="file" class="form-control" accept="video/*"
                    onchange="handleVideoUpload(${index}, this)">
                <small class="form-text text-muted">
                    Kích thước tối đa: 100MB. Định dạng hỗ trợ: MP4, WebM, OGG, MOV, AVI, MKV
                </small>
                ${content.videoUrl ? `
                    <div class="video-preview mt-3">
                        <video controls style="max-width:100%; max-height:300px;">
                            <source src="${content.videoUrl}" type="video/mp4">
                            Trình duyệt không hỗ trợ.
                        </video>
                    </div>
                ` : ''}
            </div>
        `;
    } else if (content.contentType === 'FlashcardSet') {
        // Initialize flashcards array if not exists
        if (!content.flashcards) {
            content.flashcards = [];
        }
        
        let flashcardsHTML = '';
        content.flashcards.forEach((card, cardIndex) => {
            flashcardsHTML += `
                <div class="flashcard-item" data-card-index="${cardIndex}" style="background: #f9fafb; padding: 1rem; margin-bottom: 1rem; border-radius: 8px; border: 2px solid #e5e7eb;">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem;">
                        <strong>Thẻ ${cardIndex + 1}</strong>
                        <button type="button" class="icon-btn danger" onclick="removeFlashcard(${index}, ${cardIndex})" title="Xóa thẻ">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                    <div class="form-group">
                        <label>Mặt trước:</label>
                        <textarea class="form-control flashcard-front" rows="2" onchange="updateFlashcard(${index}, ${cardIndex}, 'front', this.value)">${card.frontText || ''}</textarea>
                    </div>
                    <div class="form-group">
                        <label>Mặt sau:</label>
                        <textarea class="form-control flashcard-back" rows="2" onchange="updateFlashcard(${index}, ${cardIndex}, 'back', this.value)">${card.backText || ''}</textarea>
                    </div>
                    <div class="form-group">
                        <label>Gợi ý (tùy chọn):</label>
                        <input type="text" class="form-control flashcard-hint" value="${card.hint || ''}" onchange="updateFlashcard(${index}, ${cardIndex}, 'hint', this.value)">
                    </div>
                </div>
            `;
        });
        
        return `
            <div class="form-group">
                <label>Thông tin Flashcard Set:</label>
                <input type="text" class="form-control flashcard-set-title" placeholder="Tiêu đề bộ flashcard" value="${content.flashcardSetTitle || ''}" onchange="updateFlashcardSetTitle(${index}, this.value)" style="margin-bottom: 1rem;">
                <textarea class="form-control flashcard-set-desc" rows="2" placeholder="Mô tả ngắn về bộ flashcard" onchange="updateFlashcardSetDesc(${index}, this.value)">${content.flashcardSetDesc || ''}</textarea>
            </div>
            <div class="flashcards-container" id="flashcardsContainer_${index}">
                ${flashcardsHTML}
            </div>
            <button type="button" class="btn btn-outline-primary" onclick="addFlashcard(${index})" style="width: 100%;">
                <i class="fas fa-plus"></i> Thêm thẻ flashcard
            </button>
        `;
    } else if (content.contentType === 'Test') {
        // Initialize questions array if not exists
        if (!content.questions) {
            content.questions = [];
        }
        
        let questionsHTML = '';
        content.questions.forEach((question, qIndex) => {
            let optionsHTML = '';
            if (question.options) {
                question.options.forEach((option, oIndex) => {
                    optionsHTML += `
                        <div class="option-item" style="display: flex; gap: 0.5rem; margin-bottom: 0.5rem; align-items: center;">
                            <input type="checkbox" ${option.isCorrect ? 'checked' : ''} onchange="updateQuestionOption(${index}, ${qIndex}, ${oIndex}, 'isCorrect', this.checked)" style="width: 20px; height: 20px;">
                            <input type="text" class="form-control" value="${option.optionText || ''}" onchange="updateQuestionOption(${index}, ${qIndex}, ${oIndex}, 'text', this.value)" placeholder="Nội dung đáp án">
                            <button type="button" class="icon-btn danger" onclick="removeQuestionOption(${index}, ${qIndex}, ${oIndex})" title="Xóa đáp án">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    `;
                });
            }
            
            questionsHTML += `
                <div class="test-question-item" data-question-index="${qIndex}" style="background: #f9fafb; padding: 1.5rem; margin-bottom: 1.5rem; border-radius: 8px; border: 2px solid #e5e7eb;">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem;">
                        <strong style="font-size: 1.1rem;">Câu hỏi ${qIndex + 1}</strong>
                        <button type="button" class="icon-btn danger" onclick="removeTestQuestion(${index}, ${qIndex})" title="Xóa câu hỏi">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                    <div class="form-group">
                        <label>Loại câu hỏi:</label>
                        <select class="form-control" onchange="updateQuestionType(${index}, ${qIndex}, this.value)">
                            <option value="MCQ_Single" ${question.type === 'MCQ_Single' ? 'selected' : ''}>Trắc nghiệm (1 đáp án)</option>
                            <option value="MCQ_Multi" ${question.type === 'MCQ_Multi' ? 'selected' : ''}>Trắc nghiệm (nhiều đáp án)</option>
                            <option value="TrueFalse" ${question.type === 'TrueFalse' ? 'selected' : ''}>Đúng/Sai</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label>Câu hỏi:</label>
                        <textarea class="form-control" rows="3" onchange="updateQuestionText(${index}, ${qIndex}, this.value)">${question.stemText || ''}</textarea>
                    </div>
                    <div class="form-group">
                        <label>Điểm:</label>
                        <input type="number" class="form-control" value="${question.points || 1}" min="0.5" step="0.5" onchange="updateQuestionPoints(${index}, ${qIndex}, this.value)" style="max-width: 120px;">
                    </div>
                    <div class="form-group">
                        <label>Các đáp án: <small class="text-muted">(Chọn checkbox cho đáp án đúng)</small></label>
                        <div class="options-container" id="optionsContainer_${index}_${qIndex}">
                            ${optionsHTML}
                        </div>
                        <button type="button" class="btn btn-sm btn-outline-primary" onclick="addQuestionOption(${index}, ${qIndex})" style="margin-top: 0.5rem;">
                            <i class="fas fa-plus"></i> Thêm đáp án
                        </button>
                    </div>
                </div>
            `;
        });
        
        return `
            <div class="form-group">
                <label>Thông tin bài test:</label>
                <input type="text" class="form-control test-title" placeholder="Tiêu đề bài test" value="${content.testTitle || ''}" onchange="updateTestTitle(${index}, this.value)" style="margin-bottom: 1rem;">
                <textarea class="form-control test-desc" rows="2" placeholder="Mô tả về bài test" onchange="updateTestDesc(${index}, this.value)">${content.testDesc || ''}</textarea>
            </div>
            <div class="form-group" style="display: grid; grid-template-columns: 1fr 1fr; gap: 1rem;">
                <div>
                    <label>Thời gian (phút):</label>
                    <input type="number" class="form-control test-time" value="${content.timeLimitMinutes || 30}" min="1" max="1440" onchange="updateTestTime(${index}, this.value)">
                    <small class="form-text text-muted">Tối thiểu: 1 phút, Tối đa: 1440 phút (24 giờ)</small>
                </div>
                <div>
                    <label>Số lần làm tối đa:</label>
                    <input type="number" class="form-control test-attempts" value="${content.maxAttempts || 3}" min="1" max="100" onchange="updateTestAttempts(${index}, this.value)">
                    <small class="form-text text-muted">Tối thiểu: 1 lần</small>
                </div>
            </div>
            <div class="test-questions-container" id="testQuestionsContainer_${index}">
                ${questionsHTML}
            </div>
            <button type="button" class="btn btn-outline-primary" onclick="addTestQuestion(${index})" style="width: 100%;">
                <i class="fas fa-plus"></i> Thêm câu hỏi
            </button>
        `;
    }
    return '';
}

function toggleContentBody(index) {
    const contentItem = document.querySelector(`.content-item[data-content-index="${index}"]`);
    if (contentItem) {
        const body = contentItem.querySelector('.content-body');
        const icon = contentItem.querySelector('.content-header button i');

        if (body.style.display === 'none') {
            body.style.display = 'block';
            icon.classList.replace('fa-chevron-down', 'fa-chevron-up');
        } else {
            body.style.display = 'none';
            icon.classList.replace('fa-chevron-up', 'fa-chevron-down');
        }
    }
}

function updateContentType(index, type) {
    const currentLesson = window.currentLesson;
    if (currentLesson) {
        const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[index];
        content.contentType = type;

        // Cập nhật badge
        const contentItem = document.querySelector(`.content-item[data-content-index="${index}"]`);
        if (contentItem) {
            const badge = contentItem.querySelector('.content-type-badge');
            badge.className = `content-type-badge ${type.toLowerCase()}`;
            badge.textContent = getContentTypeLabel(type);

            // Cập nhật nội dung fields
            const fieldsContainer = contentItem.querySelector('.content-type-fields');
            const contentId = `content_${Date.now()}_${index}`;
            fieldsContainer.innerHTML = renderContentTypeFields(content, contentId, index);

            // Initialize CKEditor nếu chuyển sang Theory
            if (type === 'Theory') {
                setTimeout(() => {
                    initializeContentBodyEditor(contentId);
                }, 100);
            }
        }
    }
}

function updateContentTitle(index, title) {
    const currentLesson = window.currentLesson;
    if (currentLesson) {
        courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[index].title = title;
    }
}

// Ham xu ly khi tai video
function handleVideoUpload(index, input) {
    const file = input.files[0];
    if (!file) return;

    // Kiểm tra loại file 
    if (!file.type.startsWith('video/')) {
        toastr.error('Vui lòng chọn file video hợp lệ');
        input.value = '';
        return;
    }

    // Kiểm tra kích thước file (100MB)
    const maxSize = 100 * 1024 * 1024; // 100MB in bytes
    if (file.size > maxSize) {
        toastr.error('Kích thước video không được vượt quá 100MB');
        input.value = '';
        return;
    }

    // Tạo FormData để upload
    const formData = new FormData();
    formData.append('video', file);

    // Upload video với XMLHttpRequest để track progress
    const xhr = new XMLHttpRequest();

    // ✅ TẠO 1 TOAST DUY NHẤT VÀ LƯU REFERENCE
    let progressToast = null;
    let lastUpdateTime = 0;

    // Track upload progress
    xhr.upload.addEventListener('progress', (e) => {
        if (e.lengthComputable) {
            const percentComplete = Math.round((e.loaded / e.total) * 100);
            const now = Date.now();

            // ✅ CHỈ CẬP NHẬT MỖI 100MS ĐỂ TRÁNH SPAM
            if (now - lastUpdateTime < 100) return;
            lastUpdateTime = now;

            // ✅ NẾU CHƯA CÓ TOAST, TẠO MỚI
            if (!progressToast) {
                progressToast = toastr.info(`Đang tải video lên... ${percentComplete}%`, 'Thông báo', {
                    timeOut: 0,
                    extendedTimeOut: 0,
                    closeButton: false,
                    tapToDismiss: false,
                    progressBar: true
                });
            } else {
                // ✅ CẬP NHẬT NỘI DUNG TOAST HIỆN TẠI
                const messageElement = progressToast.find('.toast-message');
                if (messageElement.length) {
                    messageElement.text(`Đang tải video lên... ${percentComplete}%`);
                }
                // Cập nhật progress bar nếu có
                const progressBar = progressToast.find('.toast-progress');
                if (progressBar.length) {
                    progressBar.css('width', `${100 - percentComplete}%`);
                }
            }
        }
    });

    // Handle completion
    xhr.addEventListener('load', () => {
        // ✅ XÓA TOAST PROGRESS
        if (progressToast) {
            toastr.clear(progressToast);
            progressToast = null;
        }

        if (xhr.status === 200) {
            try {
                const data = JSON.parse(xhr.responseText);

                if (data.success && data.videoUrl) {
                    toastr.success('Tải video lên thành công!');

                    // Lưu URL video vào courseData
                    const currentLesson = window.currentLesson;
                    if (currentLesson) {
                        courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[index].videoUrl = data.videoUrl;
                    }

                    // Reload để hiển thị video preview
                    loadLessonContents();
                } else {
                    toastr.error(data.message || 'Có lỗi xảy ra khi tải video lên');
                    input.value = '';
                }
            } catch (error) {
                console.error('Error parsing response:', error);
                toastr.error('Có lỗi xảy ra khi xử lý phản hồi từ server');
                input.value = '';
            }
        } else {
            toastr.error(`Lỗi server: ${xhr.status} - ${xhr.statusText}`);
            input.value = '';
        }
    });

    // Handle errors
    xhr.addEventListener('error', () => {
        if (progressToast) {
            toastr.clear(progressToast);
            progressToast = null;
        }
        console.error('Upload error');
        toastr.error('Có lỗi xảy ra khi tải video lên');
        input.value = '';
    });

    // Handle abort
    xhr.addEventListener('abort', () => {
        if (progressToast) {
            toastr.clear(progressToast);
            progressToast = null;
        }
        toastr.warning('Upload bị hủy');
        input.value = '';
    });

    // Open connection and send
    xhr.open('POST', '/courses/upload-video');

    // Add anti-forgery token
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    if (token) {
        xhr.setRequestHeader('RequestVerificationToken', token);
    }

    xhr.send(formData);
}

function handlePdfUpload(index, input) {
    const file = input.files[0];
    if (!file) return;

    // Check file type
    if (file.type !== 'application/pdf' && !file.name.toLowerCase().endsWith('.pdf')) {
        toastr.error('Vui lòng chọn file PDF hợp lệ');
        input.value = '';
        return;
    }

    // Check file size (50MB)
    const maxSize = 50 * 1024 * 1024;
    if (file.size > maxSize) {
        toastr.error('Kích thước tài liệu không được vượt quá 50MB');
        input.value = '';
        return;
    }

    // FormData
    const formData = new FormData();
    formData.append('pdf', file);

    // XMLHttpRequest
    const xhr = new XMLHttpRequest();
    let progressToast = null;
    let lastUpdateTime = 0;

    // Track progress
    xhr.upload.addEventListener('progress', (e) => {
        if (e.lengthComputable) {
            const percentComplete = Math.round((e.loaded / e.total) * 100);
            const now = Date.now();

            if (now - lastUpdateTime < 100) return;
            lastUpdateTime = now;

            if (!progressToast) {
                progressToast = toastr.info(`Đang tải tài liệu lên... ${percentComplete}%`, 'Thông báo', {
                    timeOut: 0,
                    extendedTimeOut: 0,
                    closeButton: false,
                    tapToDismiss: false,
                    progressBar: true
                });
            } else {
                const messageElement = progressToast.find('.toast-message');
                if (messageElement.length) {
                    messageElement.text(`Đang tải tài liệu lên... ${percentComplete}%`);
                }
                const progressBar = progressToast.find('.toast-progress');
                if (progressBar.length) {
                    progressBar.css('width', `${100 - percentComplete}%`);
                }
            }
        }
    });

    // Handle completion
    xhr.addEventListener('load', () => {
        if (progressToast) {
            toastr.clear(progressToast);
            progressToast = null;
        }

        if (xhr.status === 200) {
            try {
                const data = JSON.parse(xhr.responseText);
                if (data.success && data.pdfUrl) {
                    toastr.success('Tải tài liệu lên thành công!');

                    const currentLesson = window.currentLesson;
                    if (currentLesson) {
                        courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[index].documentUrl = data.pdfUrl;
                    }
                    loadLessonContents();
                } else {
                    toastr.error(data.message || 'Có lỗi xảy ra khi tải tài liệu lên');
                    input.value = '';
                }
            } catch (error) {
                console.error('Error parsing response:', error);
                toastr.error('Có lỗi xảy ra khi xử lý phản hồi từ server');
                input.value = '';
            }
        } else {
            toastr.error(`Lỗi server: ${xhr.status} - ${xhr.statusText}`);
            input.value = '';
        }
    });

    // Handle errors
    xhr.addEventListener('error', () => {
        if (progressToast) {
            toastr.clear(progressToast);
            progressToast = null;
        }
        toastr.error('Có lỗi xảy ra khi tải tài liệu lên');
        input.value = '';
    });

    // Handle abort
    xhr.addEventListener('abort', () => {
        if (progressToast) {
            toastr.clear(progressToast);
            progressToast = null;
        }
        toastr.warning('Upload bị hủy');
        input.value = '';
    });

    xhr.open('POST', '/courses/upload-pdf');

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    if (token) {
        xhr.setRequestHeader('RequestVerificationToken', token);
    }

    xhr.send(formData);
}

function removePdfFile(index) {
    const currentLesson = window.currentLesson;
    if (currentLesson) {
        courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[index].documentUrl = null;
        loadLessonContents();
    }
}

function removeContent(index) {
    if (confirm('Bạn có chắc muốn xóa nội dung này?')) {
        const currentLesson = window.currentLesson;
        if (currentLesson) {
            courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents.splice(index, 1);
            loadLessonContents();
        }
    }
}

function getContentTypeLabel(type) {
    const labels = {
        'Theory': 'Lý thuyết',
        'Video': 'Video',
        'FlashcardSet': 'Flashcard',
        'Test': 'Kiểm tra'
    };
    return labels[type] || type;
}

// ============================================
// FLASHCARD FUNCTIONS
// ============================================

function addFlashcard(contentIndex) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    
    if (!content.flashcards) {
        content.flashcards = [];
    }
    
    const cardIndex = content.flashcards.length;
    content.flashcards.push({
        frontText: '',
        backText: '',
        hint: '',
        orderIndex: cardIndex
    });
    
    // ✅ CHỈ THÊM FLASHCARD MỚI VÀO DOM THAY VÌ RELOAD TOÀN BỘ
    const flashcardsContainer = document.getElementById(`flashcardsContainer_${contentIndex}`);
    if (flashcardsContainer) {
        const newCardHTML = `
            <div class="flashcard-item" data-card-index="${cardIndex}" style="background: #f9fafb; padding: 1rem; margin-bottom: 1rem; border-radius: 8px; border: 2px solid #e5e7eb;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem;">
                    <strong>Thẻ ${cardIndex + 1}</strong>
                    <button type="button" class="icon-btn danger" onclick="removeFlashcard(${contentIndex}, ${cardIndex})" title="Xóa thẻ">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
                <div class="form-group">
                    <label>Mặt trước:</label>
                    <textarea class="form-control flashcard-front" rows="2" onchange="updateFlashcard(${contentIndex}, ${cardIndex}, 'front', this.value)"></textarea>
                </div>
                <div class="form-group">
                    <label>Mặt sau:</label>
                    <textarea class="form-control flashcard-back" rows="2" onchange="updateFlashcard(${contentIndex}, ${cardIndex}, 'back', this.value)"></textarea>
                </div>
                <div class="form-group">
                    <label>Gợi ý (tùy chọn):</label>
                    <input type="text" class="form-control flashcard-hint" onchange="updateFlashcard(${contentIndex}, ${cardIndex}, 'hint', this.value)">
                </div>
            </div>
        `;

        flashcardsContainer.insertAdjacentHTML('beforeend', newCardHTML);
    }
}

function removeFlashcard(contentIndex, cardIndex) {
    if (!confirm('Bạn có chắc muốn xóa thẻ này?')) return;
    
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    content.flashcards.splice(cardIndex, 1);
    
    // Reload để cập nhật UI
    loadLessonContents();
}

function updateFlashcard(contentIndex, cardIndex, field, value) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    
    if (field === 'front') {
        content.flashcards[cardIndex].frontText = value;
    } else if (field === 'back') {
        content.flashcards[cardIndex].backText = value;
    } else if (field === 'hint') {
        content.flashcards[cardIndex].hint = value;
    }
}

function updateFlashcardSetTitle(contentIndex, value) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    content.flashcardSetTitle = value;
}

function updateFlashcardSetDesc(contentIndex, value) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    content.flashcardSetDesc = value;
}

// ============================================
// TEST FUNCTIONS
// ============================================

function addTestQuestion(contentIndex) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    
    if (!content.questions) {
        content.questions = [];
    }
    
    const qIndex = content.questions.length;
    content.questions.push({
        type: 'MCQ_Single',
        stemText: '',
        points: 1,
        orderIndex: qIndex,
        options: [
            { optionText: '', isCorrect: false, orderIndex: 0 },
            { optionText: '', isCorrect: false, orderIndex: 1 }
        ]
    });
    
    // ✅ CHỈ THÊM QUESTION MỚI VÀO DOM THAY VÌ RELOAD TOÀN BỘ
    const questionsContainer = document.getElementById(`testQuestionsContainer_${contentIndex}`);
    if (questionsContainer) {
        const question = content.questions[qIndex];
        let optionsHTML = '';
        question.options.forEach((option, oIndex) => {
            optionsHTML += `
                <div class="option-item" style="display: flex; gap: 0.5rem; margin-bottom: 0.5rem; align-items: center;">
                    <input type="checkbox" ${option.isCorrect ? 'checked' : ''} onchange="updateQuestionOption(${contentIndex}, ${qIndex}, ${oIndex}, 'isCorrect', this.checked)" style="width: 20px; height: 20px;">
                    <input type="text" class="form-control" value="${option.optionText || ''}" onchange="updateQuestionOption(${contentIndex}, ${qIndex}, ${oIndex}, 'text', this.value)" placeholder="Nội dung đáp án">
                    <button type="button" class="icon-btn danger" onclick="removeQuestionOption(${contentIndex}, ${qIndex}, ${oIndex})" title="Xóa đáp án">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            `;
        });
        
        const newQuestionHTML = `
            <div class="test-question-item" data-question-index="${qIndex}" style="background: #f9fafb; padding: 1.5rem; margin-bottom: 1.5rem; border-radius: 8px; border: 2px solid #e5e7eb;">
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem;">
                    <strong style="font-size: 1.1rem;">Câu hỏi ${qIndex + 1}</strong>
                    <button type="button" class="icon-btn danger" onclick="removeTestQuestion(${contentIndex}, ${qIndex})" title="Xóa câu hỏi">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
                <div class="form-group">
                    <label>Loại câu hỏi:</label>
                    <select class="form-control" onchange="updateQuestionType(${contentIndex}, ${qIndex}, this.value)">
                        <option value="MCQ_Single" ${question.type === 'MCQ_Single' ? 'selected' : ''}>Trắc nghiệm (1 đáp án)</option>
                        <option value="MCQ_Multi" ${question.type === 'MCQ_Multi' ? 'selected' : ''}>Trắc nghiệm (nhiều đáp án)</option>
                        <option value="TrueFalse" ${question.type === 'TrueFalse' ? 'selected' : ''}>Đúng/Sai</option>
                    </select>
                </div>
                <div class="form-group">
                    <label>Câu hỏi:</label>
                    <textarea class="form-control" rows="3" onchange="updateQuestionText(${contentIndex}, ${qIndex}, this.value)">${question.stemText || ''}</textarea>
                </div>
                <div class="form-group">
                    <label>Điểm:</label>
                    <input type="number" class="form-control" value="${question.points || 1}" min="0.5" step="0.5" onchange="updateQuestionPoints(${contentIndex}, ${qIndex}, this.value)" style="max-width: 120px;">
                </div>
                <div class="form-group">
                    <label>Các đáp án: <small class="text-muted">(Chọn checkbox cho đáp án đúng)</small></label>
                    <div class="options-container" id="optionsContainer_${contentIndex}_${qIndex}">
                        ${optionsHTML}
                    </div>
                    <button type="button" class="btn btn-sm btn-outline-primary" onclick="addQuestionOption(${contentIndex}, ${qIndex})" style="margin-top: 0.5rem;">
                            <i class="fas fa-plus"></i> Thêm đáp án
                        </button>
                </div>
            </div>
        `;
        questionsContainer.insertAdjacentHTML('beforeend', newQuestionHTML);
    }
}

function removeTestQuestion(contentIndex, questionIndex) {
    if (!confirm('Bạn có chắc muốn xóa câu hỏi này?')) return;
    
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    content.questions.splice(questionIndex, 1);
    
    // Reload để cập nhật UI
    loadLessonContents();
}

function updateQuestionType(contentIndex, questionIndex, value) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    content.questions[questionIndex].type = value;
    
    // ✅ CHỈ CẬP NHẬT LOẠI CÂU HỎI MÀ KHÔNG RELOAD (vì chỉ thay đổi behavior của checkboxes)
    // Không cần reload UI
}

function updateQuestionText(contentIndex, questionIndex, value) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    content.questions[questionIndex].stemText = value;
}

function updateQuestionPoints(contentIndex, questionIndex, value) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    content.questions[questionIndex].points = parseFloat(value) || 1;
}

function addQuestionOption(contentIndex, questionIndex) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    const question = content.questions[questionIndex];
    
    if (!question.options) {
        question.options = [];
    }
    
    const oIndex = question.options.length;
    question.options.push({
        optionText: '',
        isCorrect: false,
        orderIndex: oIndex
    });
    
    // ✅ CHỈ THÊM OPTION MỚI VÀO DOM THAY VÌ RELOAD TOÀN BỘ
    const optionsContainer = document.getElementById(`optionsContainer_${contentIndex}_${questionIndex}`);
    if (optionsContainer) {
        const newOptionHTML = `
            <div class="option-item" style="display: flex; gap: 0.5rem; margin-bottom: 0.5rem; align-items: center;">
                <input type="checkbox" onchange="updateQuestionOption(${contentIndex}, ${questionIndex}, ${oIndex}, 'isCorrect', this.checked)" style="width: 20px; height: 20px;">
                <input type="text" class="form-control" onchange="updateQuestionOption(${contentIndex}, ${questionIndex}, ${oIndex}, 'text', this.value)" placeholder="Nội dung đáp án">
                <button type="button" class="icon-btn danger" onclick="removeQuestionOption(${contentIndex}, ${questionIndex}, ${oIndex})" title="Xóa đáp án">
                    <i class="fas fa-trash"></i>
                </button>
            </div>
        `;
        optionsContainer.insertAdjacentHTML('beforeend', newOptionHTML);
    }
}

function removeQuestionOption(contentIndex, questionIndex, optionIndex) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    const question = content.questions[questionIndex];
    
    // Đảm bảo ít nhất 2 đáp án
    if (question.options.length <= 2) {
        toastr.warning('Câu hỏi phải có ít nhất 2 đáp án');
        return;
    }
    
    question.options.splice(optionIndex, 1);
    
    // Reload để cập nhật UI
    loadLessonContents();
}

function updateQuestionOption(contentIndex, questionIndex, optionIndex, field, value) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    const option = content.questions[questionIndex].options[optionIndex];
    
    if (field === 'text') {
        option.optionText = value;
    } else if (field === 'isCorrect') {
        // Nếu là MCQ_Single, bỏ chọn các đáp án khác
        const question = content.questions[questionIndex];
        if (question.type === 'MCQ_Single' && value === true) {
            question.options.forEach((opt, idx) => {
                opt.isCorrect = idx === optionIndex;
            });
            // ✅ CẬP NHẬT LẠI CHECKBOXES TRÊN UI
            const optionsContainer = document.getElementById(`optionsContainer_${contentIndex}_${questionIndex}`);
            if (optionsContainer) {
                const checkboxes = optionsContainer.querySelectorAll('input[type="checkbox"]');
                checkboxes.forEach((cb, idx) => {
                    cb.checked = idx === optionIndex;
                });
            }
        } else {
            option.isCorrect = value;
        }
    }
}

function updateTestTitle(contentIndex, value) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    content.testTitle = value;
}

function updateTestDesc(contentIndex, value) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    content.testDesc = value;
}

function updateTestTime(contentIndex, value) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    // ✅ VALIDATE: Kiểm tra không được âm và không được 0
    const timeValue = parseInt(value) || 30;
    if (timeValue < 1) {
        toastr.error('Thời gian làm bài phải lớn hơn 0 phút');
        // Reset lại giá trị input về 30 (default)
        const testTimeInput = document.querySelector(`.content-item[data-content-index="${contentIndex}"] .test-time`);
        if (testTimeInput) {
            testTimeInput.value = 30;
        }
        return;
    }
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    content.timeLimitMinutes = timeValue;
}

function updateTestAttempts(contentIndex, value) {
    const currentLesson = window.currentLesson;
    if (!currentLesson) return;
    
    // ✅ VALIDATE: Kiểm tra không được âm và không được 0
    const attemptsValue = parseInt(value) || 3;
    if (attemptsValue < 1) {
        toastr.error('Số lần làm bài tối đa phải lớn hơn 0');
        // Reset lại giá trị input về 3 (default)
        const testAttemptsInput = document.querySelector(`.content-item[data-content-index="${contentIndex}"] .test-attempts`);
        if (testAttemptsInput) {
            testAttemptsInput.value = 3;
        }
        return;
    }
    
    const content = courseData.chapters[currentLesson.chapterIndex].lessons[currentLesson.lessonIndex].contents[contentIndex];
    content.maxAttempts = attemptsValue;
}

// ============================================
// PREVIEW
// ============================================
function renderPreview() {
    saveStepData();

    const previewContainer = document.getElementById('previewContainer');

    let totalLessons = 0;
    let totalContents = 0;

    courseData.chapters.forEach(chapter => {
        totalLessons += chapter.lessons.length;
        chapter.lessons.forEach(lesson => {
            totalContents += lesson.contents ? lesson.contents.length : 0;
        });
    });

    let chaptersHTML = '';
    courseData.chapters.forEach(chapter => {
        let lessonsHTML = '<div class="preview-lessons">';

        // Add a no-content message if chapter has no lessons
        if (!chapter.lessons || chapter.lessons.length === 0) {
            lessonsHTML += `
                <div class="preview-lesson no-content">
                    <i class="fas fa-info-circle"></i> Chương này chưa có bài học nào.
                </div>
            `;
        } else {
            chapter.lessons.forEach(lesson => {
                lessonsHTML += `                    
                    <div class="preview-lesson">
                        <i class="fas fa-book"></i> ${lesson.title} (${lesson.contents ? lesson.contents.length : 0} nội dung)
                    </div>
                `;
            });
        }
        lessonsHTML += '</div>';

        chaptersHTML += `
            <div class="preview-chapter">
                <div class="preview-chapter-title">
                    <i class="fas fa-folder"></i> ${chapter.title}
                </div>
                ${lessonsHTML}
            </div>
        `;
    });

    previewContainer.innerHTML = `
        <div class="preview-section">
            <h3>Thông tin khóa học</h3>
            <div class="preview-grid">
                <div class="preview-item">
                    <div class="preview-label">Tiêu đề:</div>
                    <div class="preview-value">${courseData.title}</div>
                </div>
                <div class="preview-item">
                    <div class="preview-label">Slug:</div>
                    <div class="preview-value">${courseData.slug}</div>
                </div>
                <div class="preview-item">
                    <div class="preview-label">Giá:</div>
                    <div class="preview-value">${courseData.price === 0 ? 'Miễn phí' : courseData.price.toLocaleString('vi-VN') + ' VNĐ'}</div>
                </div>
                <div class="preview-item">
                    <div class="preview-label">Trạng thái:</div>
                    <div class="preview-value">${courseData.isPublished ? 'Xuất bản' : 'Bản nháp'}</div>
                </div>
            </div>
        </div>
        
        <div class="preview-section">
            <h3>Tổng quan cấu trúc</h3>
            <div class="preview-grid">
                <div class="preview-item">
                    <div class="preview-label">Số chương:</div>
                    <div class="preview-value">${courseData.chapters.length}</div>
                </div>
                <div class="preview-item">
                    <div class="preview-label">Số bài học:</div>
                    <div class="preview-value">${totalLessons}</div>
                </div>
                <div class="preview-item">
                    <div class="preview-label">Tổng nội dung:</div>
                    <div class="preview-value">${totalContents}</div>
                </div>
            </div>
        </div>
        
        <div class="preview-section">
            <h3>Cấu trúc khóa học</h3>
            ${chaptersHTML}
        </div>
    `;
}

// ============================================
// SAVE COURSE
// ============================================
function saveCourse(publish) {
    // ✅ LƯU DỮ LIỆU CỦA LESSON HIỆN TẠI NẾU CÓ
    if (window.currentLesson) {
        saveCurrentLessonContents();
    }

    saveStepData();

    // Update publish status
    courseData.isPublished = publish;

    // Prepare form data
    const form = document.getElementById('courseBuilderForm');
    const jsonDataInput = document.getElementById('jsonDataInput');
    jsonDataInput.value = JSON.stringify(courseData);

    // Set form action based on whether it's create or update
    const courseId = new URLSearchParams(window.location.search).get('id');
    if (courseId) {
        form.action = `/courses/builder/update/${courseId}`;
    } else {
        form.action = '/courses/builder/save';
    }

    // Submit the form
    form.submit();
}

// ============================================
// AUTOSAVE
// ============================================
//function startAutosave() {
    
//    // Autosave every 10 seconds
//    autosaveTimer = setInterval(() => {
//        if (currentStep === 1) {
//            performAutosave();
//        }
//    }, 10000);
//}

async function performAutosave() {
    saveStepData();

    // Skip autosave when slug is invalid or not confirmed available for current value
    const slug = courseData.slug?.trim();
    if (!slug || !/^[a-z0-9-]+$/.test(slug)) return;
    if (lastSlugCheck.value !== slug || lastSlugCheck.available !== true || lastSlugCheck.loading) return;

    const autosaveData = {
        courseId: new URLSearchParams(window.location.search).get('id'),
        title: courseData.title,
        slug: courseData.slug,
        summary: courseData.summary,
        categoryId: courseData.categoryId,
        coverUrl: courseData.coverUrl,
        price: courseData.price,
        isPublished: courseData.isPublished
    };

    try {
        const res = await fetch('/courses/builder/autosave', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify(autosaveData),
            credentials: 'same-origin'
        });

        let data = {};
        try { data = await res.json(); } catch { /* no json body */ }

        // If API signals duplicate slug, show UI error and mark cache
        if (res.status === 409 && (data?.code === 'DuplicateSlug' || data?.message)) {
            showFieldError('Slug', 'Slug này đã tồn tại');
            lastSlugCheck = { value: autosaveData.slug, available: false, loading: false };
            notifyAndFocusSlug('Slug này đã tồn tại, vui lòng chọn slug khác.', autosaveData.slug);
            return;
        }

        if (!res.ok || data?.success === false) {
            console.warn('Autosave failed', data);
            return;
        }

        if (data?.success) {
            showAutosaveStatus();
        }
    } catch (error) {
        console.error('Autosave error:', error);
    }
}

function showAutosaveStatus() {
    const status = document.querySelector('.autosave-status');
    status.style.display = 'flex';

    setTimeout(() => {
        status.style.display = 'none';
    }, 3000);
}

// ============================================
// LOAD EXISTING DATA
// ============================================
function loadExistingData(data) {
    courseData = data;

    // Load step 1 data
    document.getElementById('Title').value = data.title || '';
    document.getElementById('Slug').value = data.slug || '';

    const categorySelect = document.getElementById('CategoryId');
    if (data.categoryId && categorySelect) {
        categorySelect.value = data.categoryId;
    }

    document.getElementById('Price').value = data.price || 0;
    document.getElementById('CoverUrl').value = data.coverUrl || '';

    if (data.coverUrl) {
        const preview = document.getElementById('imagePreview');
        const img = preview.querySelector('img');
        img.src = data.coverUrl;
        preview.style.display = 'block';
    }

    // Load CKEditor data after initialization
    setTimeout(() => {
        if (ckEditorInstances['Summary'] && data.summary) {
            ckEditorInstances['Summary'].setData(data.summary);
        }
    }, 500);

    // Load chapters and lessons
    if (data.chapters && data.chapters.length > 0) {
        data.chapters.forEach(chapter => {
            addChapter();
            const chapterItems = document.querySelectorAll('.chapter-item');
            const lastChapterItem = chapterItems[chapterItems.length - 1];
            const chapterId = lastChapterItem.dataset.chapterId;

            lastChapterItem.querySelector('.chapter-title-input').value = chapter.title;

            // Load chapter description
            setTimeout(() => {
                const chapterDescEditor = ckEditorInstances[`chapterDesc_${chapterId}`];
                if (chapterDescEditor && chapter.description) {
                    chapterDescEditor.setData(chapter.description);
                }
            }, 500);

            // Load lessons
            if (chapter.lessons && chapter.lessons.length > 0) {
                chapter.lessons.forEach(lesson => {
                    addLesson(chapterId);
                    const lessonItems = lastChapterItem.querySelectorAll('.lesson-item');
                    const lastLessonItem = lessonItems[lessonItems.length - 1];

                    lastLessonItem.querySelector('.lesson-title-input').value = lesson.title;
                    lastLessonItem.querySelector('.lesson-visibility').value = lesson.visibility;
                });
            }
        });
    }
}
