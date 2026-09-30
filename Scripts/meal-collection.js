(function () {
    var root = document.getElementById('mc-root');
    if (!root) return;

    var verifyUrl = '/MealCollection/Verify';
    var recordUrl = '/MealCollection/Record';

    var video = document.getElementById('video');
    var placeholder = document.getElementById('placeholder');
    var startBtn = document.getElementById('startBtn');
    var captureBtn = document.getElementById('captureBtn');
    var status = document.getElementById('status');
    var resultCard = document.getElementById('resultCard');
    var resultName = document.getElementById('resultName');
    var resultMeta = document.getElementById('resultMeta');
    var confidenceFill = document.getElementById('confidenceFill');
    var confidenceLabel = document.getElementById('confidenceLabel');
    var warningBox = document.getElementById('warningBox');
    var mealSlotLabel = document.getElementById('mealSlotLabel');
    var mealName = document.getElementById('mealName');
    var allergenInfo = document.getElementById('allergenInfo');
    var confirmBtn = document.getElementById('confirmBtn');
    var cancelBtn = document.getElementById('cancelBtn');
    var slotButtons = document.querySelectorAll('.mc-slot-btn');

    var stream = null;
    var currentSlot = 'Breakfast';
    var currentMatch = null;

    // ---- Slot picker ----
    slotButtons.forEach(function (btn) {
        btn.addEventListener('click', function () {
            slotButtons.forEach(function (b) { b.classList.remove('active'); });
            btn.classList.add('active');
            currentSlot = btn.getAttribute('data-slot');
            hideResult();
        });
    });

    function setStatus(msg, cls) {
        if (!status) return;
        status.className = 'mc-status ' + (cls || 'info');
        status.textContent = msg;
        status.style.display = 'block';
    }

    function hideResult() {
        if (resultCard) resultCard.classList.remove('visible', 'ok', 'warn', 'err');
    }

    function startCamera() {
        if (!video) return;

        navigator.mediaDevices.getUserMedia({ video: { width: 640, height: 480 } })
            .then(function (s) {
                stream = s;
                video.srcObject = s;
                if (placeholder) placeholder.style.display = 'none';
                if (startBtn) startBtn.style.display = 'none';
                if (captureBtn) captureBtn.style.display = 'inline-block';
                setStatus('Camera ready. Position face and click Capture & Verify.', 'info');
            })
            .catch(function (err) {
                setStatus('Camera error: ' + err.message, 'err');
            });
    }

    function capturePhoto() {
        if (!video || !video.videoWidth) {
            setStatus('Camera not ready.', 'err');
            return;
        }

        var canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d').drawImage(video, 0, 0);

        var dataUrl = canvas.toDataURL('image/jpeg', 0.8);

        setStatus('Verifying...', 'info');
        hideResult();

        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        var tokenValue = token ? token.value : '';

        fetch(verifyUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': tokenValue
            },
            body: '__RequestVerificationToken=' + encodeURIComponent(tokenValue)
                + '&imageBase64=' + encodeURIComponent(dataUrl)
                + '&mealSlot=' + encodeURIComponent(currentSlot)
        })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (!data.success) {
                    if (data.alreadyCollected) {
                        showAlreadyCollected(data);
                        return;
                    }
                    setStatus(data.message || 'Verification failed.', 'err');
                    return;
                }

                showMatch(data);
            })
            .catch(function (err) {
                setStatus('Server error: ' + err.message, 'err');
            });
    }

    function showMatch(data) {
        setStatus('Match found.', 'ok');

        currentMatch = data;

        resultName.textContent = data.studentName;
        resultMeta.textContent = 'Student #' + data.studentId;
        mealSlotLabel.textContent = data.mealSlot;
        mealName.textContent = data.mealName;

        var confidence = Math.round((data.confidence || 0) * 100);
        confidenceFill.style.width = confidence + '%';
        confidenceFill.style.background = confidence >= 80 ? '#10b981' : (confidence >= 60 ? '#f59e0b' : '#ef4444');
        confidenceLabel.innerHTML = 'Match: <strong>' + confidence + '%</strong> (distance ' + data.matchDistance + ')';

        if (data.hasAllergenConflict) {
            warningBox.style.display = 'block';
            warningBox.className = 'mc-warning allergen';
            warningBox.textContent = '⚠ ALLERGEN CONFLICT — Student is allergic to ' + data.studentAllergies;
            resultCard.classList.add('err');
        } else {
            warningBox.style.display = 'none';
            resultCard.classList.add('ok');
        }

        if (data.allergens) {
            allergenInfo.textContent = 'Contains: ' + data.allergens;
        } else {
            allergenInfo.textContent = '';
        }

        resultCard.classList.add('visible');
    }

    function showAlreadyCollected(data) {
        setStatus('Already collected.', 'err');

        currentMatch = null;

        resultName.textContent = data.studentName;
        resultMeta.textContent = '';
        mealSlotLabel.textContent = currentSlot;
        mealName.textContent = '(already collected)';
        confidenceFill.style.width = '0%';
        confidenceLabel.innerHTML = '';

        warningBox.style.display = 'block';
        warningBox.className = 'mc-warning already';
        warningBox.textContent = '⚠ Already collected at ' + data.collectedAt;

        allergenInfo.textContent = '';
        confirmBtn.style.display = 'none';

        resultCard.classList.remove('ok', 'err');
        resultCard.classList.add('warn', 'visible');
    }

    function confirmCollection() {
        if (!currentMatch) return;

        var token = document.querySelector('input[name="__RequestVerificationToken"]');
        var tokenValue = token ? token.value : '';

        var body = '__RequestVerificationToken=' + encodeURIComponent(tokenValue)
            + '&studentId=' + currentMatch.studentId
            + '&mealSlot=' + encodeURIComponent(currentMatch.mealSlot)
            + '&method=FaceMatch'
            + '&confidence=' + (currentMatch.confidence || 0);

        fetch(recordUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': tokenValue
            },
            body: body
        })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data.success) {
                    setStatus('Collected at ' + data.collectedAt + '. Refreshing page...', 'ok');
                    setTimeout(function () { window.location.reload(); }, 1200);
                } else {
                    setStatus(data.message || 'Failed to record.', 'err');
                }
            })
            .catch(function (err) {
                setStatus('Server error: ' + err.message, 'err');
            });
    }

    // Wire up buttons
    if (startBtn) startBtn.addEventListener('click', startCamera);
    if (captureBtn) captureBtn.addEventListener('click', capturePhoto);
    if (confirmBtn) confirmBtn.addEventListener('click', confirmCollection);
    if (cancelBtn) cancelBtn.addEventListener('click', function () {
        hideResult();
        setStatus('Cancelled. Ready for next student.', 'info');
        confirmBtn.style.display = 'inline-block';
    });
})();