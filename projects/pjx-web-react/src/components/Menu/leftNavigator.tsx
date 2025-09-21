import React from 'react';
import clsx from 'clsx';
import { createStyles, Theme, withStyles, WithStyles } from '@material-ui/core/styles';
import Divider from '@material-ui/core/Divider';
import Drawer, { DrawerProps } from '@material-ui/core/Drawer';
import { Link } from 'react-router-dom';
import List from '@material-ui/core/List';
import ListItem from '@material-ui/core/ListItem';
import ListItemIcon from '@material-ui/core/ListItemIcon';
import ListItemText from '@material-ui/core/ListItemText';
import HomeIcon from '@material-ui/icons/Home';
import PersonAddIcon from '@material-ui/icons/PersonAdd';
import AirplanemodeActiveIcon from '@material-ui/icons/AirplanemodeActive';
import CommuteIcon from '@material-ui/icons/Commute';
import PermMediaOutlinedIcon from '@material-ui/icons/PhotoSizeSelectActual';
import SettingsEthernetIcon from '@material-ui/icons/SettingsEthernet';
import PowerOffIcon from '@material-ui/icons/PowerOff';
import SettingsIcon from '@material-ui/icons/Settings';
import CalendarTodayIcon from '@material-ui/icons/CalendarToday';
import { Omit } from '@material-ui/types';
import { IDENTITY_CONFIG } from '../../utils/authConst';
import { AuthConsumer } from "../../providers/authProvider";

const styles = (theme: Theme) =>
  createStyles({
    categoryHeader: {
      paddingTop: theme.spacing(2),
      paddingBottom: theme.spacing(2),
    },
    categoryHeaderPrimary: {
      color: theme.palette.common.white,
    },
    item: {
      paddingTop: 1,
      paddingBottom: 1,
      color: 'rgba(255, 255, 255, 0.7)',
      '&:hover,&:focus': {
        backgroundColor: 'rgba(255, 255, 255, 0.08)',
      },
    },
    itemCategory: {
      backgroundColor: '#232f3e',
      boxShadow: '0 -1px 0 #404854 inset',
      paddingTop: theme.spacing(2),
      paddingBottom: theme.spacing(2),
    },
    itemLogo: {
      fontSize: 24,
      textDecoration: 'none',
      color: theme.palette.common.white,
    },
    itemUnderLogo: {
      fontSize: 18,
      textDecoration: 'none',
      color: theme.palette.common.white,
    },
    itemActiveItem: {
      color: '#4fc3f7',
    },
    itemPrimary: {
      fontSize: 'inherit',
    },
    itemIcon: {
      minWidth: 'auto',
      marginRight: theme.spacing(2),
    },
    divider: {
      marginTop: theme.spacing(2),
    },
  });

export interface LeftNavigatorProps {
  isLoggedIn?: boolean;
}

function LeftNavigator(props: LeftNavigatorProps & Omit<DrawerProps, 'classes'> & WithStyles<typeof styles>) {
  const { classes, ...other } = props;

  const renderLoggedIn = () => {
    const categories = [
      {
        main: 'Development',
        children: [
          { id: 'Home', icon: <HomeIcon />, active: true, to: '/' },
          { id: 'Country List', icon: <AirplanemodeActiveIcon />, to: '/country/all' },
          { id: 'City List', icon: <CommuteIcon />, to: '/cities' },
          { id: 'Calendar', icon: <CalendarTodayIcon />, to: '/calendar' },
          { id: 'Dashboard', icon: <PermMediaOutlinedIcon />, to: '/dashboard' },
        ],
      },
      {
        main: 'My Account',
        children: [
          { id: 'Profile', icon: <PersonAddIcon />, redirect: `${IDENTITY_CONFIG.profile}?returnUrl=${encodeURIComponent('' + process.env.REACT_APP_PUBLIC_URL)}` },
          { id: 'Sign Out', icon: <PowerOffIcon />, to: '/logout' },
        ],
      },
    ];

    return renderSection(categories);
  }

  const renderLoggedOut = () => {
    const categories = [
      {
        main: 'Development',
        children: [
          { id: 'Home', icon: <HomeIcon />, active: true, to: '/' }
        ],
      },
      {
        main: 'My Account',
        children: [
          { id: 'Register', icon: <PersonAddIcon />, to: '/register' },
          { id: 'Sign On', icon: <SettingsIcon />, to: '/dashboard' }
        ],
      },
    ];

    return renderSection(categories);
  }

  const renderSection = (section: any) => {
    return (
      <React.Fragment>
        {section.map((eachSec: { main: string, children: any }) => (
          <React.Fragment key={eachSec.main}>
            <ListItem className={classes.categoryHeader}>
              <ListItemText
                classes={{
                  primary: classes.categoryHeaderPrimary,
                }}
              >
                {eachSec.main}
              </ListItemText>
            </ListItem>
            {eachSec.children.map((subSec: { id: string, icon: any, active?: boolean, to: string, redirect: string }) => (
              <ListItem
                key={subSec.id}
                button
                className={clsx(classes.item, subSec.active && classes.itemActiveItem)}
              >
                <ListItemIcon className={classes.itemIcon}>{subSec.icon}</ListItemIcon>
                <ListItemText
                  classes={{
                    primary: classes.itemPrimary,
                  }}
                >
                  {subSec.to && (
                    <Link className={clsx(classes.item, subSec.active && classes.itemActiveItem)} to={subSec.to}>{subSec.id}</Link>
                  )}
                  {subSec.redirect && (
                    <a className={clsx(classes.item, subSec.active && classes.itemActiveItem)} href={subSec.redirect} rel="noopener noreferrer">{subSec.id}</a>
                  )}             
                </ListItemText>
              </ListItem>
            ))}
            <Divider className={classes.divider} />
          </React.Fragment> 
        ))}
      </React.Fragment>
    );
  }

  return (
    <Drawer variant="permanent" {...other}>
      <List disablePadding>
        <ListItem>
          <a className={clsx(classes.itemLogo)} target="_blank" href="https://github.com/users/mikelau13/projects/1" rel="noopener noreferrer">
              Pjx
          </a>
        </ListItem>
        <ListItem className={clsx(classes.item, classes.itemCategory)}>
          <ListItemIcon className={classes.itemIcon}>
            <SettingsEthernetIcon />
          </ListItemIcon>
          <ListItemText>
            <a className={clsx(classes.itemUnderLogo)} target="_blank" href="https://www.github.com/mikelau13/pjx-root/" rel="noopener noreferrer">
                Git: Pjx Project
            </a>
          </ListItemText>
        </ListItem>
        <AuthConsumer>
            {({ isAuthenticated }) => {
                if (isAuthenticated()) {
                  return renderLoggedIn();
                } else {
                  return renderLoggedOut();
                }
            }}
        </AuthConsumer>
      </List>
    </Drawer>
  );
}

export default withStyles(styles)(LeftNavigator);
