﻿        function NewsViewModel() {
            var self = this;
            self.color = ko.observable('unassigned');
            self.ourEnergy = ko.observable(1);
            self.theirEnergy = ko.observable(1);
            self.movesRemaining = ko.observable(0);
            self.update = function (moveRequest) {
                var playerName = moveRequest.player;
                var color = playerName === 'p1' ? 'red' : 'blue';
                self.color(color);
                var ourInfo = color === 'red' ? moveRequest.state.p1 : moveRequest.state.p2;
                var theirInfo = color === 'blue' ? moveRequest.state.p1 : moveRequest.state.p2;
                self.ourEnergy(ourInfo.energy);
                self.theirEnergy(theirInfo.energy);
                self.movesRemaining(moveRequest.state.maxTurns - moveRequest.state.turnsElapsed);
            }

        }

    $(function () {
        var news = $.connection.warViewHub;
        news.client.sendLatestMove = function (warViewJson) {
            var warviewModel = JSON.parse(warViewJson);
            showTurn(warviewModel);
            //warNews.update(data);
        };
        $.connection.hub.start().done(function () { });
    });

    function setPlayerCount(grid) {
        var redCount = 0;
        var blueCount = 0;
        for (var i = 0; i < grid.length; i++) {
            var myChar = grid[i];
            if (myChar == '1') {
                redCount++;
            }
            else if (myChar == '2') {
                blueCount++;
            }
        }
        $('#blue-count').text(blueCount);
        $('#red-count').text(redCount);
    }

        function setWinner(state) {
            if (state.winner != null) {
                $('#blue-wrap').removeClass('alert-info');
                $('#red-wrap').removeClass('alert-danger');
                if (state.winner == 'p1') {
                    $('#red-game-end-message').text("Winner!");
                    $('#blue-game-end-message').text("Loser!");
                    $('#red-wrap').addClass('alert-success');
                    $('#blue-wrap').addClass('alert-warning');
                } else if (state.winner == 'p2') {
                    $('#blue-game-end-message').text("Winner!");
                    $('#red-game-end-message').text("Loser!");
                    $('#blue-wrap').addClass('alert-success');
                    $('#red-wrap').addClass('alert-warning');
                }
            } else {
                $('#red-wrap').removeClass('alert-success alert-warning');
                $('#blue-wrap').removeClass('alert-success alert-warning');
                $('#red-game-end-message').text("");
                $('#blue-game-end-message').text("");
                $('#red-wrap').addClass('alert-danger');
                $('#blue-wrap').addClass('alert-info');
            }
        }


        function showTurn(warViewModel) {
        showAnyAlerts(warViewModel.alert);
        var state = warViewModel.state;
        setPlayerCount(state.grid);
        $('#red-energy').text(state.p1.energy);
        $('#blue-energy').text(state.p2.energy);
        $("#red-name").text(warViewModel.p1Name);
        $("#blue-name").text(warViewModel.p2Name);
        setWinner(state);
        $('#turn-number').text(state.turnsElapsed + " / " + state.maxTurns);
        ctx.clearRect(0, 0, c.width, c.height);
        ctx.strokeStyle = 'lightgrey';
        var coordWidth = c.width / state.cols;
        for (var i = 1; i < state.cols; i++) {
            var x = i * coordWidth;
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, c.height);
            ctx.stroke();
        }

        var coordHeight = c.height / state.rows;
        for (var i = 1; i < state.rows; i++) {
            var y = i * coordHeight;
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(c.width, y);
            ctx.stroke();
        }
        if (!state.p1.spawnDisabled) {
            var p1Spawn = indexToCoord(state, state.p1.spawn);
            ctx.fillStyle = 'rgba(242, 97, 64, 0.5)';
            ctx.beginPath();
            ctx.rect(p1Spawn.x * coordWidth, p1Spawn.y * coordHeight, coordWidth, coordHeight);
            ctx.fill();
        }
        if (!state.p2.spawnDisabled) {
            var p2Spawn = indexToCoord(state, state.p2.spawn);
            ctx.beginPath();
            ctx.fillStyle = 'rgba(110, 161, 215, 0.5)';
            ctx.rect(p2Spawn.x * coordWidth, p2Spawn.y * coordHeight, coordWidth, coordHeight);
            ctx.fill();
        }

        var p1Headcount = 0;
        var p2Headcount = 0;
        for (var i = 0; i < state.grid.length; i++) {
            var gridId = state.grid[i];
            if (gridId !== '.') {
                var coord = indexToCoord(state, i);
                var x = coord.x * coordWidth + coordWidth / 2;
                var y = coord.y * coordHeight + coordHeight / 2;

                switch (gridId) {
                    case '1':
                        p1Headcount++;
                        ctx.fillStyle = '#F26140';
                        ctx.beginPath();
                        ctx.arc(x, y, coordWidth / 2 - 2, 0, 2 * Math.PI);
                        ctx.fill();
                        break;
                    case '2':
                        p2Headcount++;
                        ctx.fillStyle = '#6EA1D7';
                        ctx.beginPath();
                        ctx.arc(x, y, coordWidth / 2 - 2, 0, 2 * Math.PI);
                        ctx.fill();
                        break;
                    case '*':
                        ctx.drawImage(energyImage, 679, 51, 94, 94, x - coordWidth / 2, y - coordHeight / 2, coordWidth, coordHeight);
                        break;
                    case 'x':
                        ctx.fillStyle = '#F26140';
                        ctx.beginPath();
                        ctx.arc(x, y, coordWidth / 2 - 2, 0, 2 * Math.PI);
                        ctx.fill();
                        ctx.strokeStyle = 'black';
                        ctx.beginPath();
                        ctx.moveTo(x - coordWidth / 4, y - coordWidth / 4);
                        ctx.lineTo(x + coordWidth / 4, y + coordWidth / 4);
                        ctx.stroke();
                        ctx.beginPath();
                        ctx.moveTo(x - coordWidth / 4, y + coordWidth / 4);
                        ctx.lineTo(x + coordWidth / 4, y - coordWidth / 4);
                        ctx.stroke();
                        break;
                    case 'X':
                        ctx.fillStyle = '#6EA1D7';
                        ctx.beginPath();
                        ctx.arc(x, y, coordWidth / 2 - 2, 0, 2 * Math.PI);
                        ctx.fill();
                        ctx.strokeStyle = 'black';
                        ctx.beginPath();
                        ctx.moveTo(x - coordWidth / 4, y - coordWidth / 4);
                        ctx.lineTo(x + coordWidth / 4, y + coordWidth / 4);
                        ctx.stroke();
                        ctx.beginPath();
                        ctx.moveTo(x - coordWidth / 4, y + coordWidth / 4);
                        ctx.lineTo(x + coordWidth / 4, y - coordWidth / 4);
                        ctx.stroke();
                        break;
                    default:
                        console.log(gridId);
                }
            }
        }
        $('#p1 .headcount').html(p1Headcount);
        $('#p2 .headcount').html(p2Headcount);
        $('#p1 .energyconsumed').html(state.p1.energy);
        $('#p2 .energyconsumed').html(state.p2.energy);
    }

    function indexToCoord(state, index) {
        var x = index % state.cols;
        var y = ~~(index / state.cols);
        return { x: x, y: y };
    }

    function showAnyAlerts(alert) {
        if (alert != null && alert.length > 0) {
            var alertDiv = $('<div />', {
                "class": 'alert alert-danger alert-dismissable',
                "role": 'alert',
                id: 'game-alert'
            });
            var button = $('<button/>', {
                "class": "close",
                "type": "button",
                "data-dismiss": "alert"
            });
            var span1 = $('<span/>', {
                "aria-hidden": "true",
                text: "x"
            });
            var span2 = $('<span/>', {
                "class": "sr-only",
                text: "Close"
            });
            var text = $('<p/>', {
                text: alert
            });
            button.append(span1);
            button.append(span2);
            alertDiv.append(button);
            alertDiv.append(text);
            $("#flavor-text").append(alertDiv);
        }
}

    var ctx;
    var energyImage;
    c = document.getElementById('game');
    ctx = c.getContext('2d');
    energyImage = document.getElementById("energy-image");
    energyImage.onload = function () {
        showTurn({
            rows: 20, cols: 20, maxTurns: 0, turnsElapsed: 0,
            grid: "................................................................................................................................................................................................................................................................................................................................................................................................................",
            maxTurns: 200,
            p1: {
                energy: 1,
                spawn: 21
            },
            p2: {
                energy: 1,
                spawn: 378
            }
        });
    }
    window.warNews = new NewsViewModel();
    ko.applyBindings(window.warNews);