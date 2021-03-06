﻿require.config({
    paths: {
        'text': '../App/durandal/amd/text',
        //'durandal': '../App/durandal',
        'plugins': '../App/durandal/plugins',
        'transitions': '../App/durandal/transitions',
        //'knockout': '../scripts/knockout-3.1.0',
        'knockout.validation': '../scripts/knockout.validation',
        'bootstrap': '../scripts/bootstrap',
        'jquery': '../scripts/jquery-2.1.0',
        'jquery.utilities': '../scripts/jquery.utilities',
        'datepicker': '../scripts/bootstrap-datepicker'
    }
});

define('knockout', ko);

define(['durandal/system', 'durandal/app', 'durandal/plugins/router', 'durandal/viewLocator', 'services/logger', 'services/session', 'knockout', 'knockout.validation'],
    function (system, app, router, viewLocator, logger, session, ko) {
    system.debug(true);

    app.title = 'Hop&Scotch Attendance';

    app.configurePlugins({
        router: true
    });

    configureKnockout();

    app.start().then(function () {
        //router.useConvention();
        viewLocator.useConvention();

        app.setRoot('viewmodels/shell', 'entrance');

        router.handleInvalidRoute = function (route, params) {
            logger.logError('No route found', route, 'main', true);
        };
    });

    function configureKnockout() {
        ko.validation.init({
            insertMessage: true,
            decorateElement: true,
            errorElementClass: 'has-error',
            errorMessageClass: 'help-block'
        });

        if (!ko.utils.cloneNodes) {
            ko.utils.cloneNodes = function (nodesArray, shouldCleanNodes) {
                for (var i = 0, j = nodesArray.length, newNodesArray = []; i < j; i++) {
                    var clonedNode = nodesArray[i].cloneNode(true);
                    newNodesArray.push(shouldCleanNodes ? ko.cleanNode(clonedNode) : clonedNode);
                }
                return newNodesArray;
            };
        }

        ko.bindingHandlers.date = {
            init: function(element, valueAccessor, allBindingsAccessor) {
                var el = $(element);
                var options = allBindingsAccessor().datepickerOptions || { language: 'ru', autoclose: true };
                
                el.datepicker(options)
                    .on('changeDate', function() {
                        var observable = valueAccessor();
                        observable(el.datepicker('getDate'));
                    });
                
                ko.utils.domNodeDisposal.addDisposeCallback(element, function() {
                    el.datepicker('remove');
                });
            },
            update: function (element, valueAccessor, allBindingsAccessor, viewModel) {
                var el = $(element);
                var value = valueAccessor();
                var allBindings = allBindingsAccessor();
                var valueUnwrapped = ko.utils.unwrapObservable(value);
                var pattern = allBindings.format || 'DD/MM/YYYY';
                var output = "-";
                if (valueUnwrapped !== null && valueUnwrapped !== undefined) {
                    output = moment(valueUnwrapped).format(pattern);
                }
                
                if (el.is("input") === true) {
                    el.val(output);
                } else {
                    el.text(output);
                }

                el.datepicker('update', valueUnwrapped);
            }
        };

        ko.bindingHandlers.ifIsInRole = {
            init: function (element, valueAccessor, allBindingsAccessor, viewModel, bindingContext) {
                ko.utils.domData.set(element, '__ko_withIfBindingData', {});
                return { 'controlsDescendantBindings': true };
            },
            update: function (element, valueAccessor, allBindingsAccessor, viewModel, bindingContext) {
                var withIfData = ko.utils.domData.get(element, '__ko_withIfBindingData'),
                    dataValue = ko.utils.unwrapObservable(valueAccessor()),
                    shouldDisplay = session.userIsInRole(dataValue),
                    isFirstRender = !withIfData.savedNodes,
                    needsRefresh = isFirstRender || (shouldDisplay !== withIfData.didDisplayOnLastUpdate),
                    makeContextCallback = false;

                if (needsRefresh) {
                    if (isFirstRender) {
                        withIfData.savedNodes = ko.utils.cloneNodes(ko.virtualElements.childNodes(element), true /* shouldCleanNodes */);
                    }

                    if (shouldDisplay) {
                        if (!isFirstRender) {
                            ko.virtualElements.setDomNodeChildren(element, ko.utils.cloneNodes(withIfData.savedNodes));
                        }

                        ko.applyBindingsToDescendants(makeContextCallback ? makeContextCallback(bindingContext, dataValue) : bindingContext, element);
                    } else {
                        ko.virtualElements.emptyNode(element);
                    }

                    withIfData.didDisplayOnLastUpdate = shouldDisplay;
                }
            }
        };
    }
});