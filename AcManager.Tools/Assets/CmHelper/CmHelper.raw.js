(function (UICmHelper, $, __ACc, undefined) {
	var _container,
		_language,
		_path,
		_ui,
		_updateTimer,
		_mouseDown = false;

	/*
	The init function takes 3 parameters:
	container - the selector which contains our module's HTML (by default 'body > .modules');
				once the html is in there we can then move it around with DOM manipulation
	language - the currently selected language in the theme

	*/

	function init(container, language, options) {
		_container = container;
		_language = language;
		_path = options.path;	// the basepath to our module's assets

		render();
		bindControls();

		// append the stylesheet to the document head
		
		$('<link/>', {
			id: 'UICmHelperstyle',
			rel: 'stylesheet',
			type: 'text/css',
			href: _path + '/CmHelper.css?' + Date.now()
		}).appendTo('head');

		$.ACCall('initdata/cmhelper');
	}

	function render() {
		_ui = $('.UICmHelper', _container);

		updateHelper();
		_updateTimer = setInterval(updateHelper, 1000);

		_ui.enabledrag({
			mouseup: function (x, y) {
				__AC.setDbValue('UICmHelper.position', x + ',' + y);
				_updateTimer = setInterval(updateHelper, 1000);
			}
		}).mousedown(function() {
			clearInterval(_updateTimer);
		});

		var position = __AC.getDbValue('UICmHelper.position');

		if (position) {
			position = position.split(',');

			_ui.css('left', parseInt(position[0]))
			   .css('top', parseInt(position[1]));
		}

		_ui.appendTo('#wrapper');
	}

	// update commands stuff
	function updateHelper() {
		var current = $.ACGetData('cmhelper?command/current');
		var command = {};
		if (current != null){
			try {
				command = JSON.parse(atob(current));
			} catch (e){}
		}
		$('#UICmHelper_LastCommand', _ui).html(command.name || 'none');
		if (!command.name) return;

		$.ACSetData('cmhelper?command/current', '');
		switch (command.name){
			case 'start':
				$.ACCall('start')
				break;
			case 'achievments':
				var result = 'null';
				try {
					var result = btoa(JSON.stringify($.ACCall('getachievements', 'async=true').achievements.filter(function(a){ return a.maxTier != -1 }).reduce(function(a, b){ return a[b.name] = b.maxTier, a}, {})));
				} catch (e){}
				$.ACSetData('cmhelper?result/achievments', result)
				break;
		}
	}

	function bindControls() {
		// it's a good idea to add a namespace to the bound events [eventname].[namespace]
		// so we can can remove them if we want the module to be disposable without reloading the UI theme

		$(document)
			.off('.UICmHelper')
			.on({
				// ACRunningEvent is raised whenever the simulator starts
				'ACRunningEvent.UICmHelper': function () {
					_ui.addClass('ACactive');
					clearInterval(_updateTimer);
				},

				// ACStoppedEvent is raised whenever the simulator stops
				'ACStoppedEvent.UICmHelper': function () {
					_ui.removeClass('ACactive');
					_updateTimer = setInterval(updateHelper, 1000);
				}
			});

		/*
			Other events are:

			ACShowroomStart - showroom is started
			ACShowroomExit - showroom is exited
			
			ACSoftStoppedEvent - this is raised at a longer interval while AC is not active
			ACShowroomSoftStopped - this is raised at a longer interval while the showroom is not active
		*/
	}

	function dispose() {
		$('#UICmHelperstyle').remove();
		$('.UICmHelper').remove();
		clearInterval(_updateTimer);
		$(document).off('.UICmHelper');
		delete UICmHelper;
	}

	UICmHelper.Init = init;
	UICmHelper.Dispose = dispose;
}(window.UICmHelper = window.UICmHelper || {}, jQuery, __ACClasses));