/*
EonaCat Library
Copyright (C) 2017-2023 EonaCat (Jeroen Saey)

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License

*/

$(function () {
    $('input').click(function () {
        $('.btn input[type="checkbox"]:not(:checked)').parent().removeClass("active");
        $('.btn input[type="checkbox"]:checked').parent().addClass("active");
        $('input:not(:checked)').parent().removeClass("active");
        $('input:checked').parent().addClass("active");
    });
});

function htmlEncode(value) {
    return $("<div/>").text(value).html();
}

function htmlDecode(value) {
    return $("<div/>").html(value).text();
}

async function HTTPRequest(options) {
    var async = false;
    var successFlag = false;
    var processData = true;
    var dataContentType = 'application/x-www-form-urlencoded; charset=UTF-8';

    var finalUrl = options.url;
    var data = options.data || '';
    var success = options.success;
    var error = options.error || (() => window.location = "/");
    var invalidToken = options.invalidToken || error;
    var alert = options.alert;
    var loader = options.loader;
    var isFormData = options.isFormData;
    var alert = options.alert || false;

    if (success) {
        async = true;
    }
    if (isFormData) {
        processData = false;
        dataContentType = false;
    }

    hideAlert(alert);
    if (loader) {
        loader.html(getLoader());
    }

    try {
        const response = await $.ajax({
            type: "POST",
            url: finalUrl,
            data: data,
            dataType: "json",
            async: async,
            cache: false,
            processData: processData,
            contentType: dataContentType
        });

        if (loader) {
            loader.html("");
        }

        switch (response.status) {
            case "ok":
                if (success) {
                    success(response);
                } else {
                    successFlag = true;
                }
                break;
            case "invalid-token":
                invalidToken();
                break;
            case "error":
                showAlert("danger", "Error!", response.errorMessage, alert);
                error();
                break;
            default:
                showAlert("danger", "Invalid Response!", "The server returned an invalid response status: " + response.status, alert);
                error();
                break;
        }
    } catch (errorThrown) {
        if (loader) {
            loader.html("");
        }
        var message = errorThrown === "" ? "Unable to connect to the server. Please try again." : errorThrown;
        showAlert("danger", "Error!", message, alert);
        error();
    }

    return successFlag;
}

async function HTTPGetFileRequest(url, success, error, alert, loader, alert) {
    var options = url;
    if (typeof url === "string") {
        options = { url, success, error, alert, loader, alert };
    }
    var { url: finalUrl, success, error, alert, loader, alert } = options;

    hideAlert(alert);
    if (loader) {
        loader.html(getLoader());
    }

    try {
        const response = await fetch(finalUrl);
        if (response.ok) {
            const result = await response.text();
            if (loader) {
                loader.html("");
            }
            success && success(result);
        } else {
            throw new Error(`${response.status} - ${response.statusText}`);
        }
    } catch (err) {
        if (loader) {
            loader.html("");
        }
        error && error();
        var message = err.message === "Failed to fetch"
            ? "Unable to connect to the server. Please try again."
            : err.message;
        showAlert("danger", "Error!", message, alert);
    }
}

function getLoader() {
    return "<div style='width: 64px; height: inherit; margin: auto;'><div style='height: inherit; display: table-cell; vertical-align: middle;'><img class='rotating' src='/images/logo.svg' style='width:64px;'/> Loading...</div></div>";
}

function countdown(callback, duration, countdownInterval = 1000, updateInterval = 100) {
    const bar = document.getElementById('alertProgress');
    let startTime = null;
    function update(timeStamp) {
        if (!startTime) {
            startTime = timeStamp;
        }
        const elapsedTime = timeStamp - startTime;
        const progress = Math.floor((elapsedTime / duration) * updateInterval);
        bar.style.width = progress + '%';
        if (progress >= 100) {
            callback && setTimeout(callback, countdownInterval);
        } else {
            requestAnimationFrame(update);
        }
    }
    requestAnimationFrame(update);
}

function showAlert(type, title, message, alert, duration = 5, countdownInterval = 1000, updateInterval = 100) {
    const alertContainer = $('.EonaCatAlert');

    const alertHTML = `<div class="alert alert-${type}">
        <button type="button" class="close" data-bs-dismiss="alert">&times;</button>
        <strong>${title}</strong><br />${htmlEncode(message)}`;

    if (type === 'success') {
        const progressHTML = `<div class="progress" style="height: 5px;">
            <div id="alertProgress" class="progress-bar progress-bar-striped bg-info" role="progressbar"></div>
        </div>`;

        alertContainer.html(alertHTML + progressHTML + "</div>");
        countdown(() => hideAlert(alertContainer), duration * 1000, countdownInterval, updateInterval);
    } else {
        alertContainer.html(alertHTML + "</div>");
    }

    return true;
}

function hideAlert(alert) {
    alert = alert || $(".EonaCatAlert");
    if (alert instanceof jQuery) {
        alert.toggle();
    }
    else {
        alert.innerHTML = '';
    }
}