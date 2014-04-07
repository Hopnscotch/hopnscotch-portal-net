﻿require.config({
    paths: {
        "text": "../App/durandal/amd/text",
        //"durandal": "../App/durandal",
        "plugins": "../App/durandal/plugins",
        "transitions": "../App/durandal/transitions",
        //"knockout": "../scripts/knockout-3.1.0",
        "bootstrap": "../scripts/bootstrap",
        "jquery": "../scripts/jquery-2.1.0"
    }
});

define('knockout', ko);

define(function(require) {
    var system = require('durandal/system');
    var app = require('durandal/app');
    var router = require('durandal/plugins/router');
    var viewLocator = require('durandal/viewLocator');
    var logger = require('services/logger');

    system.debug(true);

    app.configurePlugins({
        router: true
    });

    app.start().then(function () {
        //router.useConvention();
        viewLocator.useConvention();

        app.setRoot('viewmodels/shell', 'entrance');

        router.handleInvalidRoute = function (route, params) {
            logger.logError('No route found', route, 'main', true);
        };
    });
});